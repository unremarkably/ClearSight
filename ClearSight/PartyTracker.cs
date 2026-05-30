using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using LuminaClassJob = Lumina.Excel.Sheets.ClassJob;
using LuminaStatus = Lumina.Excel.Sheets.Status;

namespace ClearSight;

// Everything the party panel needs to know about one buff/debuff on a member,
// already resolved into plain values so the UI never touches game memory.
public readonly struct StatusInfo
{
    public uint StatusId { get; init; }
    public string Name { get; init; }

    /// <summary>The in-game status icon, drawn the same as it appears on your buff bar.</summary>
    public uint IconId { get; init; }

    public float RemainingTime { get; init; }

    /// <summary>Whatever the game packed into Param — only a real count when MaxStacks > 1.</summary>
    public ushort Stacks { get; init; }

    /// <summary>How many stacks this status can hold; 1 (or 0) means it doesn't stack.</summary>
    public byte MaxStacks { get; init; }

    /// <summary>True when the local player is the one who applied this status.</summary>
    public bool Mine { get; init; }

    /// <summary>True for the Sage barriers/regens we specifically watch for.</summary>
    public bool IsTracked { get; init; }

    /// <summary>True for debuffs, false for buffs — drives the buff/debuff filters.</summary>
    public bool IsDebuff { get; init; }

    /// <summary>Always-on statuses (permanent links, FC buffs) the "hide permanent" filter drops.</summary>
    public bool IsPermanent { get; init; }
}

// A single party member as the panel sees them.
public readonly struct PartyMemberInfo
{
    public string Name { get; init; }
    public string Job { get; init; }

    /// <summary>The ClassJob row id, used to fetch the job icon.</summary>
    public uint JobId { get; init; }

    /// <summary>Used to look the member up again when you click to target them.</summary>
    public uint EntityId { get; init; }

    /// <summary>The ClassJob role: 1 tank, 2/3 dps, 4 healer, 0 other.</summary>
    public byte Role { get; init; }

    /// <summary>True for the local player's own entry.</summary>
    public bool IsSelf { get; init; }

    public uint CurrentHp { get; init; }
    public uint MaxHp { get; init; }
    public uint CurrentMp { get; init; }
    public uint MaxMp { get; init; }

    /// <summary>The yellow overshield on the HP bar, 0-100, from any source.</summary>
    public byte ShieldPercent { get; init; }

    /// <summary>True when at least one of your barriers is currently on them.</summary>
    public bool HasMyBarrier { get; init; }

    public IReadOnlyList<StatusInfo> Statuses { get; init; }

    public float HpFraction => MaxHp == 0 ? 0f : (float)CurrentHp / MaxHp;
    public float MpFraction => MaxMp == 0 ? 0f : (float)CurrentMp / MaxMp;
}

/// <summary>
/// Reads the party each frame and hands back a tidy snapshot: who's in it, how
/// hurt they are, how shielded they are, and which of our barriers are on them.
/// Knowing the source of each status is what lets the panel separate the shields
/// you cast from everyone else's.
/// </summary>
public sealed class PartyTracker
{
    // The Sage barriers and regens worth calling out. We match these against the
    // Status sheet by name so we don't depend on raw IDs that drift over patches;
    // anything that doesn't resolve is simply skipped.
    private static readonly string[] TrackedStatusNames =
    {
        "Eukrasian Diagnosis",
        "Eukrasian Prognosis",
        "Differential Diagnosis",
        "Holosakos",
        "Haimatinon",
        "Panhaimatinon",
        "Kerachole",
        "Taurochole",
        "Physis II",
        "Krasis",
    };

    private readonly IPartyList partyList;
    private readonly IObjectTable objects;
    private readonly IDataManager data;

    // Resolved once from the Status sheet: the IDs behind the names above.
    private HashSet<uint>? trackedStatusIds;

    public PartyTracker(IPartyList partyList, IObjectTable objects, IDataManager data)
    {
        this.partyList = partyList;
        this.objects = objects;
        this.data = data;
    }

    /// <summary>
    /// The current party as plain snapshots. Empty when you're not grouped.
    /// </summary>
    public List<PartyMemberInfo> Snapshot()
    {
        var tracked = ResolveTrackedIds();
        var mine = objects.LocalPlayer?.EntityId ?? 0;

        var members = new List<PartyMemberInfo>(partyList.Length);
        foreach (var member in partyList)
        {
            // Shield % lives on the battle character, which we only have when the
            // member is loaded nearby — fine in a dungeon, absent across a zone.
            var chara = member.GameObject as IBattleChara;

            // The live object's status list ticks down every frame; the party
            // array only refreshes in network steps, so its timers look frozen.
            // Prefer the live one whenever the member is loaded.
            var statusSource = chara?.StatusList ?? member.Statuses;

            var statuses = new List<StatusInfo>();
            var hasMyBarrier = false;
            foreach (var status in statusSource)
            {
                if (status.StatusId == 0)
                    continue;

                var isMine = status.SourceId == mine;
                var isTracked = tracked.Contains(status.StatusId);
                if (isMine && isTracked)
                    hasMyBarrier = true;

                var sheet = LookupStatus(status.StatusId);
                statuses.Add(new StatusInfo
                {
                    StatusId = status.StatusId,
                    Name = sheet.Name,
                    IconId = sheet.Icon,
                    RemainingTime = Math.Abs(status.RemainingTime),
                    Stacks = status.Param,
                    MaxStacks = sheet.MaxStacks,
                    Mine = isMine,
                    IsTracked = isTracked,
                    IsDebuff = sheet.IsDebuff,
                    IsPermanent = sheet.IsPermanent,
                });
            }

            var job = LookupJob(member.ClassJob.RowId);

            members.Add(new PartyMemberInfo
            {
                Name = member.Name.ToString(),
                Job = job.Abbreviation,
                JobId = member.ClassJob.RowId,
                EntityId = member.EntityId,
                Role = job.Role,
                IsSelf = member.EntityId == mine,
                CurrentHp = member.CurrentHP,
                MaxHp = member.MaxHP,
                CurrentMp = member.CurrentMP,
                MaxMp = member.MaxMP,
                ShieldPercent = chara?.ShieldPercentage ?? 0,
                HasMyBarrier = hasMyBarrier,
                Statuses = statuses,
            });
        }

        // A stable layout the eye can rely on: yourself first, then tanks,
        // healers, and dps, so a member never jumps slots mid-fight.
        members.Sort((a, b) =>
        {
            if (a.IsSelf != b.IsSelf)
                return a.IsSelf ? -1 : 1;

            var order = RoleOrder(a.Role).CompareTo(RoleOrder(b.Role));
            return order != 0 ? order : string.CompareOrdinal(a.Name, b.Name);
        });

        return members;
    }

    private static int RoleOrder(byte role) => role switch
    {
        1 => 0, // tank
        4 => 1, // healer
        2 or 3 => 2, // dps
        _ => 3,
    };

    private (string Abbreviation, byte Role) LookupJob(uint jobId)
    {
        if (data.GetExcelSheet<LuminaClassJob>().TryGetRow(jobId, out var row))
            return (row.Abbreviation.ToString(), row.Role);

        return ("", 0);
    }

    private HashSet<uint> ResolveTrackedIds()
    {
        if (trackedStatusIds != null)
            return trackedStatusIds;

        var wanted = new HashSet<string>(TrackedStatusNames, StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<uint>();

        foreach (var row in data.GetExcelSheet<LuminaStatus>())
        {
            if (wanted.Contains(row.Name.ToString()))
                ids.Add(row.RowId);
        }

        trackedStatusIds = ids;
        return ids;
    }

    private (string Name, uint Icon, bool IsDebuff, bool IsPermanent, byte MaxStacks) LookupStatus(uint statusId)
    {
        if (data.GetExcelSheet<LuminaStatus>().TryGetRow(statusId, out var row))
        {
            var name = row.Name.ToString();
            if (string.IsNullOrEmpty(name))
                name = $"#{statusId}";

            // StatusCategory 2 means debuff; 1 (and anything else) we treat as a
            // buff. FC buffs ride along with permanents so the filter sweeps them up.
            return (name, row.Icon, row.StatusCategory == 2, row.IsPermanent || row.IsFcBuff, row.MaxStacks);
        }

        return ($"#{statusId}", 0, false, false, 0);
    }
}
