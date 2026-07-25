using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using NoPasaranFC.Database;
using NoPasaranFC.Models;

namespace NoPasaranFC.Screens
{
    /// <summary>
    /// Halftime substitution screen: the lineup UI in a halftime mode. Max
    /// <see cref="MaxSubs"/> substitutions (a sub = a bench player entering the
    /// starting 11; re-adding an original starter refunds it). Enter confirms
    /// and resumes the match; Escape restores the lineup as it was at halftime.
    /// </summary>
    public class HalftimeScreen : LineupScreen
    {
        public const int MaxSubs = 3;
        
        private readonly HashSet<Player> _startersAtEntry;
        
        public HalftimeScreen(Team team, Match match, Championship championship, DatabaseManager database,
            ScreenManager screenManager, ContentManager content, GraphicsDevice graphicsDevice)
            : base(team, match, championship, database, screenManager, content, graphicsDevice)
        {
            _startersAtEntry = _allPlayers.Where(p => p.IsStarting).ToHashSet();
        }
        
        /// <summary>Subs used: current starters who were not starting at halftime.</summary>
        public int SubsUsed => _allPlayers.Count(p => p.IsStarting && !_startersAtEntry.Contains(p));
        
        protected override bool CanAddToStarting(Player player) => SubsUsed < MaxSubs;
        
        protected override string GetTitle() =>
            $"{Localization.Instance.Get("match.halftime")} ({SubsUsed}/{MaxSubs})";
        
        /// <summary>Confirm: lineup already saved by the base class; resume the match.</summary>
        protected override void OnConfirm()
        {
            IsFinished = true;
        }
        
        /// <summary>Escape: revert to the halftime lineup, then resume.</summary>
        protected override void OnCancel()
        {
            foreach (var p in _allPlayers)
                p.IsStarting = _startersAtEntry.Contains(p);
            IsFinished = true;
        }
    }
}
