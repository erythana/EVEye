using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EVEye.DataAccess.Interfaces;
using EVEye.Models.EVE.Data;
using EVEye.Models.EVE.Interfaces;
using EVEye.Models.EVEye;
using EVEye.Models.ZKillboard.Data;
using EVEye.Models.ZKillboard.Interfaces;
using Microsoft.Extensions.Logging;

namespace EVEye.DataAccess;

public class PlayerInformationDataAggregator : IPlayerInformationDataAggregator
{
    #region member fields

    private readonly IEveDataRepository _eveDataRepository;
    private readonly IZKillboardDataRepository _zKillboardDataRepository;
    private readonly ILogger<PlayerInformationDataAggregator> _logger;

    #endregion
    
    #region constructor

    public PlayerInformationDataAggregator(IEveDataRepository eveDataRepository, IZKillboardDataRepository zKillboardDataRepository, ILogger<PlayerInformationDataAggregator> logger)
    {
        _eveDataRepository = eveDataRepository;
        _zKillboardDataRepository = zKillboardDataRepository;
        _logger = logger;
    }

    #endregion

    #region interface implementation

    //TODO CLEAN UP AND TRY TO LIMIT QUERIES
    public async Task<IEnumerable<EVEyePlayerInformation>> GetAggregatedItemsFor(IEnumerable<string> players)
    {
        var results = new List<AggregatorInformation>();
        var eveNameIDMappings = await _eveDataRepository.GetIDsFrom(players);

        for (var i = 0; i < eveNameIDMappings.Characters.Count; i++)
        {
            var character = eveNameIDMappings.Characters[i];
            var currentPlayer = new EVEyePlayerInformation()
            {
                ID = character.ID,
                CharacterName = character.Name,
                CharacterImage = _eveDataRepository.GetPortraitFrom(character.ID, 32),
            };
            
            var aggregatorInfo = new AggregatorInformation
            {
                EVEyePlayerInformation = currentPlayer,
                EveCharacter = _eveDataRepository.GetCharacterInformationFor(character.ID),
                KillboardHistory = _zKillboardDataRepository.GetKillboardHistoryFor(character.ID),
                ZKillboardCharacterStatistic = _zKillboardDataRepository.GetStatisticsFrom(character.ID)
            };

            results.Add(aggregatorInfo);
        }

        Task.Run(()=> BulkUpdatePlayerInformation(results));

        return results.Select(x => x.EVEyePlayerInformation);
    }

    #endregion

    #region helper methods

    private Task BulkUpdatePlayerInformation(IEnumerable<AggregatorInformation> results)
    {
        foreach (var player in results)
        {
            player.ZKillboardCharacterStatistic.ContinueWith(zkillboardStatsTask =>
            {
                if (zkillboardStatsTask.Status != TaskStatus.RanToCompletion)
                {
                    _logger.LogError(zkillboardStatsTask.Exception, "Could not fetch information from Servers");
                    throw new InvalidOperationException("Could not fetch information from Servers");
                }

                player.EveCharacter.ContinueWith(eveCharacterTask =>
                {
                    if (eveCharacterTask.Status != TaskStatus.RanToCompletion)
                    {
                        _logger.LogError(eveCharacterTask.Exception, "Could not fetch information from Servers");
                        throw new InvalidOperationException("Could not fetch information from Servers");
                    }

                    player.EVEyePlayerInformation.SecurityStanding = eveCharacterTask.Result.SecurityStatus;
                    
                    _eveDataRepository.GetNamesFrom(GetValidCorpAllianceIDs(new[]
                    {
                        eveCharacterTask.Result.CorporationID, eveCharacterTask.Result.AllianceID
                    })).ContinueWith(nameLookupTask =>
                    {
                        if (nameLookupTask.Status != TaskStatus.RanToCompletion)
                        {
                            _logger.LogError(nameLookupTask.Exception, "Could not fetch information from Servers");
                            throw new InvalidOperationException("Could not fetch information from Servers");
                        }

                        player.EVEyePlayerInformation.CorporationName = nameLookupTask.Result.FirstOrDefault(c => c.ID == eveCharacterTask.Result.CorporationID)?.Name;
                        player.EVEyePlayerInformation.AllianceName = nameLookupTask.Result.FirstOrDefault(a => a.ID == eveCharacterTask.Result.AllianceID)?.Name;
                    });

                    player.EVEyePlayerInformation.PlayerDetails = new EVEyePlayerDetails
                    {
                        ID = player.EVEyePlayerInformation.ID,
                        DangerRatio = player.ZKillboardCharacterStatistic.Result.DangerRatio,
                        GangRatio = player.ZKillboardCharacterStatistic.Result.GangRatio,
                        ShipsDestroyed = player.ZKillboardCharacterStatistic.Result.ShipsDestroyed,
                        ShipsLost = player.ZKillboardCharacterStatistic.Result.ShipsLost,
                        SoloKills = player.ZKillboardCharacterStatistic.Result.SoloKills,
                        SoloLosses = player.ZKillboardCharacterStatistic.Result.SoloLosses,
                        LatestKillboardActivity = GetEveyeKillInformation(GetFirstDetailedKillboardInfoAsync(player)),
                        Birthdate = DateTime.Parse(eveCharacterTask.Result.Birthday)
                    };
                });

            });

        }

        return Task.CompletedTask;
    }

    private IEnumerable<int> GetValidCorpAllianceIDs(IEnumerable<int> IDs) => IDs.Where(i => i > 0);


    private Task<EVEyeKillInformation>? GetEveyeKillInformation(Task<EveDetailedKillInformation?>? latestKillboardActivity)
    {
        return latestKillboardActivity?.ContinueWith(latestKillboardActivityTask =>
        {
            if (latestKillboardActivityTask.Status != TaskStatus.RanToCompletion)
            {
                _logger.LogError(latestKillboardActivityTask.Exception, "Could not fetch information from Servers");
                throw new InvalidOperationException("Could not fetch information from Servers");
            }

            return _eveDataRepository.GetNamesFrom(new[]
            {
                latestKillboardActivityTask!.Result!.Victim.ID,
                latestKillboardActivityTask!.Result!.Victim.ShipTypeID, 
                latestKillboardActivityTask!.Result!.Attackers.First(x => x.FinalBlow).ID,
                latestKillboardActivityTask!.Result!.Attackers.First(x => x.FinalBlow).ShipTypeID,
                latestKillboardActivityTask!.Result!.Attackers.First(x => x.FinalBlow).WeaponTypeID,
                latestKillboardActivityTask!.Result!.SolarSystemID
            }).ContinueWith(nameLookupTask =>
            {
                if (nameLookupTask.Status != TaskStatus.RanToCompletion)
                {
                    _logger.LogError(nameLookupTask.Exception, "Could not fetch information from Servers");
                    throw new InvalidOperationException("Could not fetch information from Servers");
                }

                return new EVEyeKillInformation
                {
                    Date = latestKillboardActivityTask.Result!.KillDate,
                    VictimName = nameLookupTask.Result.First(i => i.ID == latestKillboardActivityTask.Result.Victim.ID).Name,
                    VictimShip = nameLookupTask.Result.First(i => i.ID == latestKillboardActivityTask.Result.Victim.ShipTypeID).Name,
                    AttackerName = nameLookupTask.Result.First(i => i.ID == latestKillboardActivityTask.Result.Attackers.First(x => x.FinalBlow).ID).Name,
                    AttackerShip = nameLookupTask.Result.First(i => i.ID == latestKillboardActivityTask.Result.Attackers.First(x => x.FinalBlow).ShipTypeID).Name,
                    AttackerGuns = nameLookupTask.Result.First(i => i.ID == latestKillboardActivityTask.Result.Attackers.First(x => x.FinalBlow).WeaponTypeID).Name,
                    SolarSystem = nameLookupTask.Result.First(i => i.ID == latestKillboardActivityTask.Result.SolarSystemID).Name
                };
            });
        }).Unwrap();
    }

    private Task<EveDetailedKillInformation?>? GetFirstDetailedKillboardInfoAsync(AggregatorInformation player, Func<ZKillboardEntry, bool>? killboardLookup = null)
    {
        Task<EveDetailedKillInformation?>? detailedKillInfo = null;
        return player.KillboardHistory?.ContinueWith(t =>
        {
            if (t.Status != TaskStatus.RanToCompletion)
            {
                _logger.LogError(t.Exception, "Could not fetch information from Servers");
                throw new InvalidOperationException("Could not fetch information from Servers");
            }
            return t.Result.FirstOrDefault(killboardLookup ?? (_ => true));
        }).ContinueWith(t =>
        {
            if (t.Status != TaskStatus.RanToCompletion)
            {
                _logger.LogError(t.Exception, "Could not fetch information from Servers");
                throw new InvalidOperationException("Could not fetch information from Servers");
            }
            
            if (t.Result is not null)
                detailedKillInfo = _eveDataRepository.GetDetailedKillInformation(t.Result.ID, t.Result.ZKillboardKill.Hash);

            return detailedKillInfo;
        })!.Unwrap();
    }
    
    #endregion
}