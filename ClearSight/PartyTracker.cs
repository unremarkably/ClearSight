using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using LuminaStatus = Lumina.Excel.Sheets.Status;

namespace ClearSight;

// Everything the party panel needs to know about one buff/debuff on a member,
// already resolved into plain values so the UI never touches game memory.
public readonly struct StatusInfo
{
    public uint StatusId { get; init; }
    public string Name { get; init; }
    public float RemainingTime { get; init; }
    public ushort Stacks { get; init; }

    /// <summary>True when the local player is the one who applied this status.</summary>
    public bool Mine { get; init; }

    /// <summary>True for the Sage barriers/regens we specifically watch for.</summary>
    public bool IsTracked { get; init; }
}

// A single party member as the panel sees them.
public readonly struct PartyMemberInfo
{
    public string Name { get; init; }
    public uint JobId { get; init; }
    public uint CurrentHp { get; init; }
    public uint MaxHp { get; init; }

    /// <summary>The yellow overshield on the HP bar, 0-100, from any source.</summary>
    public byte ShieldPercent { get; init; }

    public IReadOnlyList<StatusInfo> Statuses { get; init; }

    public float HpFraction => MaxHp == 0 ? 0f : (float)CurrentHp / MaxHp;
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
            foreach (var status in statusSource)
            {
                if (status.StatusId == 0)
                    continue;

                statuses.Add(new StatusInfo
                {
                    StatusId = status.StatusId,
                    Name = StatusName(status.StatusId),
                    RemainingTime = Math.Abs(status.RemainingTime),
                    Stacks = status.Param,
                    Mine = status.SourceId == mine,
                    IsTracked = tracked.Contains(status.StatusId),
                });
            }

            members.Add(new PartyMemberInfo
            {
                Name = member.Name.ToString(),
                JobId = member.ClassJob.RowId,
                CurrentHp = member.CurrentHP,
                MaxHp = member.MaxHP,
                ShieldPercent = chara?.ShieldPercentage ?? 0,
                Statuses = statuses,
            });
        }

        return members;
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

    private string StatusName(uint statusId)
    {
        if (data.GetExcelSheet<LuminaStatus>().TryGetRow(statusId, out var row))
        {
            var name = row.Name.ToString();
            if (!string.IsNullOrEmpty(name))
                return name;
        }

        return $"#{statusId}";
    }
}
