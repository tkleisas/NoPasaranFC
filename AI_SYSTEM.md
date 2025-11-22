# AI State Machine System

## Overview
The AI system uses a state machine architecture to control non-player characters during matches. Each player has an AIController that manages their behavior through different states.

## States

### IdleState
- **Purpose**: Player is at their home position with no immediate tasks
- **Transitions**:
  - → ChasingBall: If ball is within 800px and player should chase
  - → Positioning: If too far from home position (>200px)

### PositioningState
- **Purpose**: Player returns to their home position in the formation
- **Behavior**: Moves towards home position
- **Transitions**:
  - → ChasingBall: If should chase ball and within 600px
  - → Idle: When close to home position (<50px)

### ChasingBallState
- **Purpose**: Player actively pursues the ball
- **Behavior**: Runs directly towards ball position
- **Transitions**:
  - → Dribbling: When gains possession
  - → Positioning: If someone else is closer or ball too far (>1000px)

### DribblingState
- **Purpose**: Player has possession and moves towards opponent goal
- **Behavior**: 
  - Dribbles towards opponent goal at 70% speed
  - Makes decisions every 0.5 seconds
  - Avoids sidelines automatically
- **Decision Making**:
  - 70% chance to shoot when within 800px of goal
  - 40% chance to pass if teammate is better positioned
  - Will pass if opponent too close (<150px)
- **Transitions**:
  - → AvoidingSideline: When too close to field edges
  - → Shooting: When in shooting range and decides to shoot
  - → Passing: When decides to pass
  - → ChasingBall: When loses possession

### AvoidingSidelineState
- **Purpose**: Player with ball is near sideline and needs to redirect
- **Behavior**: 
  - Moves towards field center (60% weight) and opponent goal (40% weight)
  - Active for max 1.5 seconds
- **Transitions**:
  - → Dribbling: When returns to safe area or timeout
  - → ChasingBall: If loses possession

### PassingState
- **Purpose**: Execute a pass to a teammate
- **Behavior**: 
  - Instant state - executes pass immediately
  - Power calculated based on distance (0.3-0.9)
- **Transitions**:
  - → Positioning: Immediately after pass

### ShootingState
- **Purpose**: Execute a shot towards goal
- **Behavior**:
  - Instant state - shoots immediately
  - Power based on distance from goal (0.5-1.0)
  - Random vertical offset for aim variation
- **Transitions**:
  - → Positioning: Immediately after shot

## AIContext
The context object provides all information needed for AI decision-making:
- `BallPosition`: Current ball coordinates
- `BallVelocity`: Ball movement vector
- `NearestOpponent`: Closest opponent player
- `NearestTeammate`: Closest teammate
- `DistanceToBall`: Distance from player to ball
- `HasBallPossession`: Whether player controls the ball
- `OpponentGoalCenter`: Target goal position
- `OwnGoalCenter`: Own goal position (to avoid)
- `IsPlayerTeam`: Whether this is the human player's team
- `ClosestToBall`: Which player is closest to ball
- `ShouldChaseBall`: Whether this player should pursue the ball (anti-clustering)

## Integration with MatchEngine
The MatchEngine creates AIController instances for each non-controlled player and:
1. Builds AIContext each frame with current game state
2. Calls `AIController.Update()` for each AI player
3. Registers callbacks for pass/shoot actions to execute ball physics

## Benefits
- **Clearer behavior**: Each state has a specific purpose and clear transitions
- **No more sideline issues**: AvoidingSideline state prevents players from running off-field
- **Better decision making**: Dribbling state makes intelligent choices about passing/shooting
- **Maintainable**: Easy to debug - can see current state of each player
- **Extensible**: New states can be added easily (e.g., DefendingState, TacklingState)

## Future Enhancements
- Add DefendingState for dedicated defensive behavior
- Add InterceptingState for intercepting passes
- Add MarkingState for man-to-man marking
- Add FormationState for team-wide tactical positioning
- Add difficulty scaling (adjust decision probabilities, reaction times)
