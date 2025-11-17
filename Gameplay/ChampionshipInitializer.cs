using System;
using System.Collections.Generic;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay
{
    public static class ChampionshipInitializer
    {
        private static readonly string[] TeamNames = new[]
        {
            "NO PASARAN!",
            "Ã–¡—‘”≈À…ŸÃ¡",
            " ‘≈À",
            "NO NAME",
            "Ã«◊¡Õ… œ…",
            "¡”¡À¡√«‘œ”",
            "¡”‘≈—¡” ≈Œ¡—◊≈…ŸÕ",
            "‘«√¡Õ«‘«”"
        };
        
        private static readonly string[] FirstNames = new[]
        {
            "Juan", "Carlos", "Miguel", "Pedro", "Jose", "Luis", "Antonio", "Fernando",
            "Diego", "Manuel", "Roberto", "Pablo", "Rafael", "Jorge", "Alejandro"
        };
        
        private static readonly string[] LastNames = new[]
        {
            "Garcia", "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez",
            "Perez", "Sanchez", "Ramirez", "Torres", "Flores", "Rivera", "Cruz"
        };
        private static readonly string[] NoPasaranNames = new[]
        {
            "‘‹Í·ÒÔÚ", "Dablo", " ÔÎÔÊ›˙ÒÔ", "”ıÒ‹Ì", "Ã‹ÙÊÈÂÍ",
            "–ﬁÙÂÒ –·Ì", " ıÒÈ‹ÍÔÚ Ô Í·Î¸Ú", "”›ÒÍÈ", "¬ÒÈÙÚ",
             "ÃÔ˝„ÈÔÚ", "”Ù‹ËÈÌ"
        };
        private static readonly PlayerPosition[] NoPasaranPositions = new[]
        {
            PlayerPosition.Goalkeeper,
            PlayerPosition.Defender,
            PlayerPosition.Defender,
            PlayerPosition.Defender,
            PlayerPosition.Defender,
            PlayerPosition.Midfielder,
            PlayerPosition.Midfielder,
            PlayerPosition.Midfielder,
            PlayerPosition.Midfielder,
            PlayerPosition.Forward,
            PlayerPosition.Forward,
        };
        public static Championship CreateNewChampionship()
        {
            var championship = new Championship();
            var random = new Random();
            
            int teamId = 1;
            foreach (var teamName in TeamNames)
            {
                bool isPlayerControlled = teamName == "NO PASARAN!";
                var team = new Team(teamName, isPlayerControlled);
                team.Id = teamId++;
                if(teamName == "NO PASARAN!")
                {
                    // Create 11 specific players for NO PASARAN!
                    int index = 0;
                    foreach (var playerName in NoPasaranNames)
                    {
                        var position = NoPasaranPositions[index++];
                        var player = new Player(playerName, position);
                        // Assign random stats
                        player.Speed = random.Next(40, 90);
                        player.Shooting = random.Next(30, 95);
                        player.Passing = random.Next(30, 90);
                        player.Defending = random.Next(20, 90);
                        player.Agility = random.Next(40, 90);
                        player.Technique = random.Next(40, 90);
                        player.Stamina = random.Next(50, 95);
                        
                        team.AddPlayer(player);
                    }
                    championship.Teams.Add(team);
                    continue;
                }
                // Create 11 players for each team
                // 1 Goalkeeper
                team.AddPlayer(CreatePlayer(random, PlayerPosition.Goalkeeper));
                
                // 4 Defenders
                for (int i = 0; i < 4; i++)
                {
                    team.AddPlayer(CreatePlayer(random, PlayerPosition.Defender));
                }
                
                // 4 Midfielders
                for (int i = 0; i < 4; i++)
                {
                    team.AddPlayer(CreatePlayer(random, PlayerPosition.Midfielder));
                }
                
                // 2 Forwards
                for (int i = 0; i < 2; i++)
                {
                    team.AddPlayer(CreatePlayer(random, PlayerPosition.Forward));
                }
                
                championship.Teams.Add(team);
            }
            
            championship.GenerateFixtures();
            
            return championship;
        }
        
        private static Player CreatePlayer(Random random, PlayerPosition position)
        {
            string firstName = FirstNames[random.Next(FirstNames.Length)];
            string lastName = LastNames[random.Next(LastNames.Length)];
            string name = $"{firstName} {lastName}";
            
            var player = new Player(name, position);
            
            // Generate random stats based on position
            switch (position)
            {
                case PlayerPosition.Goalkeeper:
                    player.Speed = random.Next(30, 50);
                    player.Shooting = random.Next(20, 40);
                    player.Passing = random.Next(40, 60);
                    player.Defending = random.Next(60, 90);
                    player.Agility = random.Next(60, 85);
                    player.Technique = random.Next(40, 65);
                    player.Stamina = random.Next(70, 95);
                    break;
                    
                case PlayerPosition.Defender:
                    player.Speed = random.Next(40, 65);
                    player.Shooting = random.Next(20, 45);
                    player.Passing = random.Next(50, 70);
                    player.Defending = random.Next(65, 90);
                    player.Agility = random.Next(45, 70);
                    player.Technique = random.Next(40, 65);
                    player.Stamina = random.Next(65, 90);
                    break;
                    
                case PlayerPosition.Midfielder:
                    player.Speed = random.Next(50, 75);
                    player.Shooting = random.Next(45, 70);
                    player.Passing = random.Next(60, 90);
                    player.Defending = random.Next(45, 70);
                    player.Agility = random.Next(60, 85);
                    player.Technique = random.Next(60, 85);
                    player.Stamina = random.Next(70, 95);
                    break;
                    
                case PlayerPosition.Forward:
                    player.Speed = random.Next(60, 90);
                    player.Shooting = random.Next(60, 95);
                    player.Passing = random.Next(45, 70);
                    player.Defending = random.Next(20, 45);
                    player.Agility = random.Next(65, 90);
                    player.Technique = random.Next(60, 85);
                    player.Stamina = random.Next(60, 85);
                    break;
            }
            
            return player;
        }
    }
}
