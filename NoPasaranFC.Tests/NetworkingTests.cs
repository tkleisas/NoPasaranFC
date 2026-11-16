using System.Diagnostics;
using Microsoft.Xna.Framework;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Models;
using NoPasaranFC.Networking;
using Xunit;

namespace NoPasaranFC.Tests;

/// <summary>Protocol codecs, deterministic match setup, and a real loopback
/// server/client run that verifies both clients receive identical tick bundles
/// and simulate them to the same state.</summary>
public class NetworkingTests
{
    [Fact]
    public void Protocol_RoundTripsControlMessages()
    {
        var (version, name) = NetProtocol.DecodeHello(NetProtocol.EncodeHello("Player One"));
        Assert.Equal(NetProtocol.Version, version);
        Assert.Equal("Player One", name);

        Assert.Equal(NetSide.Away, NetProtocol.DecodeWelcome(NetProtocol.EncodeWelcome(NetSide.Away)));

        var input = new NetInput(0.5f, -0.25f, shoot: true, pass: false);
        var decodedInput = NetProtocol.DecodeInput(NetProtocol.EncodeInput(input));
        Assert.Equal(input.MoveX, decodedInput.MoveX);
        Assert.Equal(input.MoveY, decodedInput.MoveY);
        Assert.True(decodedInput.Shoot);
        Assert.False(decodedInput.Pass);

        var tick = new TickInfo
        {
            Tick = 42,
            HomeInput = input,
            AwayInput = NetInput.Zero,
            ForceStartSecondHalf = true,
            MatchTime = 17.5f,
            HomeScore = 2,
            AwayScore = 1,
            State = MatchEngine.MatchState.Playing,
            Half = 2,
        };
        var decodedTick = NetProtocol.DecodeTick(NetProtocol.EncodeTick(tick));
        Assert.Equal(tick.Tick, decodedTick.Tick);
        Assert.Equal(tick.MatchTime, decodedTick.MatchTime);
        Assert.Equal(tick.HomeScore, decodedTick.HomeScore);
        Assert.Equal(tick.AwayScore, decodedTick.AwayScore);
        Assert.True(decodedTick.ForceStartSecondHalf);
        Assert.Equal(MatchEngine.MatchState.Playing, decodedTick.State);
        Assert.Equal(2, decodedTick.Half);

        var end = NetProtocol.DecodeMatchEnd(NetProtocol.EncodeMatchEnd(new MatchEndInfo { HomeScore = 3, AwayScore = 0 }));
        Assert.Equal(3, end.HomeScore);
        Assert.Equal(0, end.AwayScore);
    }

    [Fact]
    public void Protocol_MatchStart_ShipsBothRosters()
    {
        var home = TestHelper.MakeTeam("HOMETEAM", 1, playerControlled: true);
        var away = TestHelper.MakeTeam("AWAYTEAM", 2);
        var start = new MatchStartInfo
        {
            Seed = 777,
            LocalSide = NetSide.Away,
            Settings = new NetworkSettings
            {
                Difficulty = 2,
                PlayerSpeedMultiplier = 1.5f,
                AIDecisionInterval = 0.2f,
                BallControl = "Classic",
                OffsidesEnabled = true,
                MatchDurationMinutes = 5f,
            },
            HomeTeam = home,
            AwayTeam = away,
        };

        var decoded = NetProtocol.DecodeMatchStart(NetProtocol.EncodeMatchStart(start));

        Assert.Equal(777, decoded.Seed);
        Assert.Equal(NetSide.Away, decoded.LocalSide);
        Assert.Equal(2, decoded.Settings.Difficulty);
        Assert.Equal(1.5f, decoded.Settings.PlayerSpeedMultiplier, 3);
        Assert.Equal("Classic", decoded.Settings.BallControl);
        Assert.True(decoded.Settings.OffsidesEnabled);
        Assert.Equal(home.Players.Count, decoded.HomeTeam.Players.Count);
        Assert.Equal(away.Players.Count, decoded.AwayTeam.Players.Count);
        Assert.Equal(home.Players[5].Name, decoded.HomeTeam.Players[5].Name);
        Assert.Equal(home.Players[5].ShirtNumber, decoded.HomeTeam.Players[5].ShirtNumber);
        Assert.Equal(home.Players[5].IsStarting, decoded.HomeTeam.Players[5].IsStarting);
    }

    [Fact]
    public void PrepareTeams_AssignsDeterministicIdsAndSides()
    {
        var home = TestHelper.MakeTeam("HOME", 0, playerControlled: false);
        var away = TestHelper.MakeTeam("AWAY", 0, playerControlled: true);

        NetworkMatchSetup.PrepareTeams(home, away);

        Assert.Equal(1, home.Id);
        Assert.Equal(2, away.Id);
        Assert.True(home.IsPlayerControlled);
        Assert.False(away.IsPlayerControlled);
        Assert.All(home.Players, p => Assert.Equal(home.Id, p.TeamId));
        Assert.All(away.Players, p => Assert.Equal(away.Id, p.TeamId));
        Assert.Equal(home.Players.Count, home.Players.Select(p => p.Id).Distinct().Count());
        Assert.Equal(away.Players.Count, away.Players.Select(p => p.Id).Distinct().Count());
        Assert.Empty(home.Players.Select(p => p.Id).Intersect(away.Players.Select(p => p.Id)));
    }

    [Fact]
    public void Loopback_PeersReceiveSameTicks_AndSimulateIdentically()
    {
        int? previousSeed = AIController.DeterministicSeedBase;
        MatchServer server = null;
        NetworkClient clientHome = null;
        NetworkClient clientAway = null;

        try
        {
            var home = TestHelper.MakeTeam("HOME", 1, playerControlled: true);
            var away = TestHelper.MakeTeam("AWAY", 2);
            server = new MatchServer(home, away, port: 0, seed: 424242);
            server.Start();

            clientHome = NetworkClient.Connect("127.0.0.1", server.Port, "home", timeoutMs: 5000);
            clientAway = NetworkClient.Connect("127.0.0.1", server.Port, "away", timeoutMs: 5000);

            MatchStartInfo startHome = null;
            MatchStartInfo startAway = null;
            var ticksHome = new List<TickInfo>();
            var ticksAway = new List<TickInfo>();

            var clock = Stopwatch.StartNew();
            const int wantedTicks = 60;
            while (clock.ElapsedMilliseconds < 10000 &&
                   (startHome == null || startAway == null ||
                    ticksHome.Count < wantedTicks || ticksAway.Count < wantedTicks))
            {
                clientHome.SendInput(new NetInput(1f, 0.2f, false, false));
                clientAway.SendInput(new NetInput(-1f, -0.2f, false, false));
                Drain(clientHome, ref startHome, ticksHome);
                Drain(clientAway, ref startAway, ticksAway);
                Thread.Sleep(2);
            }

            Assert.NotNull(startHome);
            Assert.NotNull(startAway);
            Assert.True(ticksHome.Count >= wantedTicks, $"home got {ticksHome.Count} ticks");
            Assert.True(ticksAway.Count >= wantedTicks, $"away got {ticksAway.Count} ticks");

            Assert.Equal(startHome.Seed, startAway.Seed);
            Assert.Equal(NetSide.Home, startHome.LocalSide);
            Assert.Equal(NetSide.Away, startAway.LocalSide);

            int count = Math.Min(ticksHome.Count, ticksAway.Count);
            for (int i = 0; i < count; i++)
            {
                Assert.Equal(ticksHome[i].Tick, ticksAway[i].Tick);
                Assert.Equal(ticksHome[i].HomeInput.MoveX, ticksAway[i].HomeInput.MoveX);
                Assert.Equal(ticksHome[i].AwayInput.MoveX, ticksAway[i].AwayInput.MoveX);
                Assert.Equal(ticksHome[i].MatchTime, ticksAway[i].MatchTime);
            }

            var engineHome = BuildClientEngine(startHome);
            var engineAway = BuildClientEngine(startAway);
            Simulate(engineHome, ticksHome, count);
            Simulate(engineAway, ticksAway, count);

            Assert.Equal(engineHome.MatchTime, engineAway.MatchTime, 3);
            Assert.Equal(engineHome.HomeScore, engineAway.HomeScore);
            Assert.Equal(engineHome.AwayScore, engineAway.AwayScore);

            // Clients must also agree with the server's authoritative tick status.
            Assert.Equal(ticksHome[count - 1].MatchTime, (float)engineHome.MatchTime, 3);
            Assert.Equal(ticksHome[count - 1].HomeScore, engineHome.HomeScore);
            Assert.Equal(ticksHome[count - 1].AwayScore, engineHome.AwayScore);
            Assert.Equal(engineHome.BallPosition.X, engineAway.BallPosition.X, 2);
            Assert.Equal(engineHome.BallPosition.Y, engineAway.BallPosition.Y, 2);

            var playerHome = engineHome.HomeTeam.Players.First(p => p.IsStarting && p.Position != PlayerPosition.Goalkeeper);
            var playerAway = engineAway.HomeTeam.Players.First(p => p.IsStarting && p.Position != PlayerPosition.Goalkeeper);
            Assert.Equal(playerHome.FieldPosition.X, playerAway.FieldPosition.X, 2);
            Assert.Equal(playerHome.FieldPosition.Y, playerAway.FieldPosition.Y, 2);
        }
        finally
        {
            clientHome?.Dispose();
            clientAway?.Dispose();
            server?.Stop();
            AIController.DeterministicSeedBase = previousSeed;
        }
    }

    private static void Drain(NetworkClient client, ref MatchStartInfo start, List<TickInfo> ticks)
    {
        while (client.TryDequeue(out var evt))
        {
            switch (evt.Kind)
            {
                case NetEventKind.Start:
                    start = evt.Start;
                    break;
                case NetEventKind.Tick:
                    ticks.Add(evt.Tick);
                    break;
                case NetEventKind.Error:
                    throw new Xunit.Sdk.XunitException(evt.Message ?? "network error");
                case NetEventKind.Disconnected:
                    throw new Xunit.Sdk.XunitException("peer disconnected");
            }
        }
    }

    private static MatchEngine BuildClientEngine(MatchStartInfo start)
    {
        start.Settings.Apply();
        var home = start.HomeTeam;
        var away = start.AwayTeam;
        NetworkMatchSetup.PrepareTeams(home, away);
        AIController.DeterministicSeedBase = start.Seed;

        var engine = new MatchEngine(home, away, 1280, 720);
        engine.SetRandomSeed(start.Seed);
        engine.CelebrationDurationOverride = NetProtocol.CelebrationDurationSeconds;
        engine.JoinPlayer2(true);
        return engine;
    }

    private static void Simulate(MatchEngine engine, List<TickInfo> ticks, int count)
    {
        var elapsed = TimeSpan.Zero;
        for (int i = 0; i < count; i++)
        {
            var tick = ticks[i];
            if (tick.ForceStartSecondHalf &&
                engine.CurrentState == MatchEngine.MatchState.HalfTime)
            {
                engine.StartSecondHalf();
            }

            elapsed += NetProtocol.FixedDelta;
            engine.Update(new GameTime(elapsed, NetProtocol.FixedDelta),
                tick.HomeInput.Move, tick.HomeInput.Shoot, tick.HomeInput.Pass,
                tick.AwayInput.Move, tick.AwayInput.Shoot, tick.AwayInput.Pass);
        }
    }
}
