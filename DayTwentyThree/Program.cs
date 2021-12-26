using System.Collections.Immutable;
using Common;
using MoreLinq;
using Priority_Queue;
using Superpower;
using Superpower.Parsers;

var pawnParser = Character.In('A', 'B', 'C', 'D').Select(x => x switch
{
    'A' => PawnType.A,
    'B' => PawnType.B,
    'C' => PawnType.C,
    'D' => PawnType.D,
    _ => throw new InvalidOperationException()
});

var stateParser = from _ in Span.EqualTo("#############").IgnoreThen(Span.WhiteSpace)
             from __ in Span.EqualTo("#...........#").IgnoreThen(Span.WhiteSpace)
             from r11 in Span.EqualTo("###").IgnoreThen(pawnParser)
             from r21 in Span.EqualTo("#").IgnoreThen(pawnParser)
             from r31 in Span.EqualTo("#").IgnoreThen(pawnParser)
             from r41 in Span.EqualTo("#").IgnoreThen(pawnParser)
             from ___ in Span.EqualTo("###").IgnoreThen(Span.WhiteSpace)
             from r12 in Span.EqualTo("#").IgnoreThen(pawnParser)
             from r22 in Span.EqualTo("#").IgnoreThen(pawnParser)
             from r32 in Span.EqualTo("#").IgnoreThen(pawnParser)
             from r42 in Span.EqualTo("#").IgnoreThen(pawnParser)
             from ____ in Span.EqualTo("#").IgnoreThen(Span.WhiteSpace)
             from _____ in Span.EqualTo("#########")
             select new State(
                 new(PawnType.A, new(new(r11)), new(new(r12))),
                 new(PawnType.B, new(new(r21)), new(new(r22))),
                 new(PawnType.C, new(new(r31)), new(new(r32))),
                 new(PawnType.D, new(new(r41)), new(new(r42))));

new Runner().Run(
    stateParser,
    initial => FindMinimumCost(initial),
    initial =>
    {
        var newInitial = initial.ConvertToLarge();
        return FindMinimumCost(newInitial);
    }
);

static int FindMinimumCost(State initial)
{
    var costs = new Dictionary<State, int>
    {
        { initial, 0 }
    };
    var estimates = new Dictionary<State, int>
    {
        { initial, initial.EstimateDistanceToFulfillment() }
    };
    var visited = new HashSet<State>();
    var queue = new SimplePriorityQueue<State, int>();
    queue.Enqueue(initial, estimates[initial]);

    while (queue.Count > 0)
    {
        var state = queue.Dequeue();

        if (!visited.Add(state))
        {
            continue;
        }

        var priorCost = costs[state];

        if (state.IsAccepting)
        {
            Console.WriteLine();
            return priorCost;
        }

        if (visited.Count % 1 == 0)
        {
            Console.Write($"{queue.Count}/{visited.Count}, {priorCost}/{estimates[state]}          ");
            Console.CursorLeft = 0;
        }

        var moves = state.GenerateAllMoves().ToList();

        foreach (var (stepCost, newState, move) in moves
            .Select(m => (move: m, newState: state.Move(m)))
            .Where(x => !visited.Contains(x.newState))
            .Select(x => (Rules.TryEvaluateMove(state, x.move), x.newState, x.move))
            .Where(x => x.Item1 is not null))
        {
            var newCost = priorCost + stepCost!.Value;
            if (!costs.TryGetValue(newState, out var oldCost) || oldCost > newCost)
            {
                costs[newState] = newCost;
                var estimate = newCost + newState.EstimateDistanceToFulfillment();

                estimates[newState] = estimate;
                queue.Enqueue(newState, estimate);
            }
        }
    }

    throw new InvalidOperationException("Did not find an accepting state.");
}

public readonly struct State : IEquatable<State>
{
    public const int CorridorSize = 11;

    public ImmutableArray<Tile> Corridor { get; init; }

    public ImmutableArray<Room> Rooms { get; init; }

    public static int CorridorEnteranceIndexOf(PawnType type) => type switch
    {
        PawnType.A => 2,
        PawnType.B => 4,
        PawnType.C => 6,
        PawnType.D => 8,
        _ => throw new InvalidOperationException()
    };

    private IEnumerable<Room> UnfulfilledRooms => Rooms.Where(r => !r.IsFulfilled);

    private IEnumerable<(Pawn pawn, PawnPosition position)> UnfulfilledPawns => Corridor
        .Select((t, i) => (t.Pawn, (PawnPosition)new PawnPositionOnTheCorridor(i)))
        .Concat(UnfulfilledRooms.SelectMany(
            r => r.Slots
                .Where(s => s.Pawn?.Type != r.Type)
                .Select((s, i) => (s.Pawn, (PawnPosition)new PawnPositionInARoom(r, i)))))
        .Where(p => p.Item1 is not null)!;

    private IEnumerable<(Pawn pawn, PawnPosition position)> PawnsOutsideFulfilledRooms => Corridor
        .Select((t, i) => (t.Pawn, (PawnPosition)new PawnPositionOnTheCorridor(i)))
        .Concat(UnfulfilledRooms.SelectMany(
            r => r.Slots
                .Select((s, i) => (s.Pawn, (PawnPosition)new PawnPositionInARoom(r, i)))))
        .Where(p => p.Item1 is not null)!;

    public bool IsAccepting => Rooms.All(r => r.IsFulfilled);

    public State(Room room1, Room room2, Room room3, Room room4)
    {
        Rooms = ImmutableArray.Create(room1, room2, room3, room4);
        Corridor = ImmutableArray.CreateRange(Enumerable.Repeat(Tile.Empty, CorridorSize));
    }

    public int EstimateDistanceToFulfillment() => 
        UnfulfilledPawns.Select(x => x.pawn.MoveCost * (x.position switch
        {
            PawnPositionOnTheCorridor { Position: var pos } => 
                Math.Abs(pos - CorridorEnteranceIndexOf(x.pawn.Type)) + 1,
            PawnPositionInARoom { Room: var room, Slot: var slot } =>
                Math.Abs(room.CorridorEnteranceIndex - CorridorEnteranceIndexOf(x.pawn.Type)) 
                    + 1
                    + slot + 1,
            _ => throw new InvalidOperationException()
        })).Sum();

    public IEnumerable<Move> GenerateAllMoves()
    {
        if (IsAccepting)
        {
            return Array.Empty<Move>();
        }

        var corridorIndices = new[] {0, 1, 3, 5, 7, 9, 10};
        var slotIndices = Enumerable.Range(0, Rooms[0].Slots.Length);
        var pawns = PawnsOutsideFulfilledRooms;
        IEnumerable<Move> corridorMoves = from i in corridorIndices
                                          from p in pawns
                                          where p.position is not PawnPositionOnTheCorridor
                                          select new CorridorMove(p.pawn, p.position, i);
        IEnumerable<Move> roomMoves = from r in UnfulfilledRooms
                                      from p in pawns
                                      from b in slotIndices
                                      where r.Type == p.pawn.Type
                                      let roomPosition = p.position as PawnPositionInARoom
                                      where roomPosition?.Room != r
                                      select new RoomMove(p.pawn, p.position, r, b);

        return corridorMoves.Concat(roomMoves);
    }

    public Room GetRoom(PawnType type) => Rooms[GetRoomIndex(type)];

    private int GetRoomIndex(in Room room) => GetRoomIndex(room.Type);

    private int GetRoomIndex(PawnType type) => type switch
    {
        PawnType.A => 0,
        PawnType.B => 1,
        PawnType.C => 2,
        PawnType.D => 3,
        _ => throw new InvalidOperationException()
    };

    public State Move(Move move)
    {
        var withoutPawn = RemovePawn(move.PawnPosition);
        var pawnTile = new Tile(move.Pawn);

        return move switch
        {
            CorridorMove { Position: var pos } =>
                withoutPawn with { Corridor = withoutPawn.Corridor.SetItem(pos, pawnTile) },
            RoomMove { Room: var room, Slot: var slot } =>
                withoutPawn with 
                { 
                    Rooms = withoutPawn.Rooms.SetItem(
                        GetRoomIndex(room), 
                        withoutPawn.Rooms[GetRoomIndex(room)].SetSlot(slot, pawnTile)) 
                },
            _ => throw new InvalidOperationException()
        };
    }

    private State RemovePawn(PawnPosition position)
    {
        return position switch
        {
            PawnPositionOnTheCorridor { Position: var pos } =>
                this with { Corridor = Corridor.SetItem(pos, Tile.Empty) },
            PawnPositionInARoom { Room: var room, Slot: var slot } =>
                this with { Rooms = Rooms.SetItem(GetRoomIndex(room), room.SetSlot(slot, Tile.Empty)) },
            _ => throw new InvalidOperationException()
        };
    }

    public override bool Equals(object? obj) => obj is State state && Equals(state);

    public bool Equals(State other) =>
        Corridor.SequenceEqual(other.Corridor) &&
        Rooms.SequenceEqual(other.Rooms);

    public override int GetHashCode()
    {
        var hashCode = new HashCode();

        foreach (var tile in Corridor)
        {
            hashCode.Add(tile);
        }
        foreach (var room in Rooms)
        {
            hashCode.Add(room);
        }

        return hashCode.ToHashCode();
    }

    public State ConvertToLarge()
    {
        var room1 = new Room(
            PawnType.A,
            Rooms[0].Slots[0],
            new Tile(new Pawn(PawnType.D)),
            new Tile(new Pawn(PawnType.D)),
            Rooms[0].Slots[1]
        );
        var room2 = new Room(
            PawnType.B,
            Rooms[1].Slots[0],
            new Tile(new Pawn(PawnType.C)),
            new Tile(new Pawn(PawnType.B)),
            Rooms[1].Slots[1]
        );
        var room3 = new Room(
            PawnType.C,
            Rooms[2].Slots[0],
            new Tile(new Pawn(PawnType.B)),
            new Tile(new Pawn(PawnType.A)),
            Rooms[2].Slots[1]
        );
        var room4 = new Room(
            PawnType.D,
            Rooms[3].Slots[0],
            new Tile(new Pawn(PawnType.A)),
            new Tile(new Pawn(PawnType.C)),
            Rooms[3].Slots[1]
        );

        return new State(room1, room2, room3, room4);
    }
}

public record class PawnPosition;

public record class PawnPositionOnTheCorridor(int Position) : PawnPosition;

public record class PawnPositionInARoom(Room Room, int Slot) : PawnPosition;

public static class Rules
{
    public static int? TryEvaluateMove(
        in State state,
        Move move)
    {
        var detailedMove = GetMoveDetails(move);

        if (detailedMove.StartAtCorridorPosition == detailedMove.StopAtCorridorPosition)
        {
            return null;
        }

        var moveValidation = new MoveValidation(detailedMove, state);

        moveValidation.IsNotCorridorOnly();
        moveValidation.DoesNotEnterARoomThatIsNotOfPawnsTypeUnlessItDoesNotExitThatRoom();
        moveValidation.DoesNotEnterARoomThatContainsPawnsOfDifferentTypeUnlessItDoesNotExitThatRoom();
        moveValidation.DoesNotStopOnARoomEnteranceIfItDoesNotEnterThatRoom();
        moveValidation.DoesNotGoThroughOccupiedTiles();

        if (moveValidation.IsValid)
        {
            var cost = detailedMove.Distance * detailedMove.Pawn.MoveCost;
            return cost;
        }

        return null;
    }

    private static DetailedMove GetMoveDetails(Move move)
    {
        var (roomFrom, slotFrom, corridorFrom) = move.PawnPosition switch
        {
            PawnPositionOnTheCorridor { Position: var from } => ((Room?)null, (int?)null, from),
            PawnPositionInARoom { Room: var room, Slot: var slot } => (room, slot, room.CorridorEnteranceIndex),
            _ => throw new InvalidOperationException()
        };
        var (roomTo, slotTo, corridorTo) = move switch
        {
            CorridorMove { Position: var to } => ((Room?)null, (int?)null, to),
            RoomMove { Room: var room, Slot: var slot } => (room, slot, room.CorridorEnteranceIndex),
            _ => throw new InvalidOperationException()
        };

        return new DetailedMove(move.Pawn, roomFrom, slotFrom, corridorFrom, corridorTo, roomTo, slotTo);
    }

    private struct MoveValidation
    {
        private readonly State _state;

        private readonly DetailedMove _move;

        public bool IsValid { get; private set; } = true;

        public MoveValidation(DetailedMove move, State state) =>
            (_move, _state) = (move, state);

        public void IsNotCorridorOnly()
        {
            if (!IsValid) return;

            IsValid = _move.FromRoom is not null || _move.IntoRoom is not null;
        }

        public void DoesNotGoThroughOccupiedTiles()
        {
            if (!IsValid) return;

            if (_move.FromRoom is not null)
            {
                for (var i = 0; i < _move.FromSlot; i += 1)
                {
                    if (_move.FromRoom.Value.Slots[i] != Tile.Empty)
                    {
                        IsValid = false;
                        return;
                    }
                }
            }

            if (_move.IntoRoom is not null)
            {
                for (var i = 0; i <= _move.IntoSlot; i += 1)
                {
                    if (_move.IntoRoom.Value.Slots[i] != Tile.Empty)
                    {
                        IsValid = false;
                        return;
                    }
                }
            }
            
            var increment = _move.StartAtCorridorPosition <= _move.StopAtCorridorPosition ? 1 : -1;

            for (var i = _move.StartAtCorridorPosition + increment; i != _move.StopAtCorridorPosition; i += increment)
            {
                if (_state.Corridor[i] != Tile.Empty)
                {
                    IsValid = false;
                    return;
                }
            }
            if (_state.Corridor[_move.StopAtCorridorPosition] != Tile.Empty)
            {
                IsValid = false;
                return;
            }
        }

        public void DoesNotStopOnARoomEnteranceIfItDoesNotEnterThatRoom()
        {
            if (!IsValid) return;

            var move = _move;

            IsValid = _state.Rooms.All(
                r => r == move.IntoRoom || move.StopAtCorridorPosition != r.CorridorEnteranceIndex);
        }

        public void DoesNotEnterARoomThatIsNotOfPawnsTypeUnlessItDoesNotExitThatRoom()
        {
            if (!IsValid) return;

            IsValid = _move.FromRoom == _move.IntoRoom
                || _move.IntoRoom is null 
                || _move.IntoRoom.Value.Type == _move.Pawn.Type;
        }

        public void DoesNotEnterARoomThatContainsPawnsOfDifferentTypeUnlessItDoesNotExitThatRoom()
        {
            if (!IsValid) return;

            var move = _move;
            IsValid = _move.FromRoom == _move.IntoRoom
                || _move.IntoRoom is null
                || _move.IntoRoom.Value.Slots.All(s => s.Pawn is null || s.Pawn.Type == move.Pawn.Type);
        }
    }
}

public record class Move(Pawn Pawn, PawnPosition PawnPosition);

public record class CorridorMove(Pawn Pawn, PawnPosition PawnPosition, int Position) : Move(Pawn, PawnPosition);

public record class RoomMove(Pawn Pawn, PawnPosition PawnPosition, Room Room, int Slot) : Move(Pawn, PawnPosition);

public readonly record struct DetailedMove(
    Pawn Pawn,
    Room? FromRoom,
    int? FromSlot,
    int StartAtCorridorPosition,
    int StopAtCorridorPosition,
    Room? IntoRoom,
    int? IntoSlot)
{
    public int Distance => DistanceToLeaveRoom + DistanceOnTheCorridor + DistanceToEnterRoom;

    private int DistanceToLeaveRoom => DistanceForSlot(FromSlot);

    private int DistanceToEnterRoom => DistanceForSlot(IntoSlot);

    private int DistanceOnTheCorridor => Math.Abs(StartAtCorridorPosition - StopAtCorridorPosition);

    private static int DistanceForSlot(int? slot) => slot is null ? 0 : slot.Value + 1;
}

public readonly struct Room : IEquatable<Room>
{
    private readonly bool _isFullfilled;
    
    private readonly ImmutableArray<Tile> _slots;

    public PawnType Type { get; init; }

    public ImmutableArray<Tile> Slots
    {
        get => _slots; 
        init
        {
            var type = Type;
            _slots = value;
            _isFullfilled = _slots.All(s => s.Pawn?.Type == type);
        }
    }

    public Room(PawnType type, Tile upperSlot, Tile lowerSlot)
    {
        Type = type;
        _isFullfilled = false;
        _slots = ImmutableArray<Tile>.Empty;
        Slots = ImmutableArray.Create(upperSlot, lowerSlot);
    }
    public Room(PawnType type, Tile slot1, Tile slot2, Tile slot3, Tile slot4)
    {
        Type = type;
        _isFullfilled = false;
        _slots = ImmutableArray<Tile>.Empty;
        Slots = ImmutableArray.Create(slot1, slot2, slot3, slot4);
    }

    public bool IsFulfilled => _isFullfilled;

    public int CorridorEnteranceIndex => State.CorridorEnteranceIndexOf(Type);

    public Room SetSlot(int slot, Tile pawnTile) => this with { Slots = Slots.SetItem(slot, pawnTile) };

    public override bool Equals(object? obj) =>
        obj is Room room && Equals(room);

    public bool Equals(Room other) =>
        Type == other.Type && Slots.SequenceEqual(other.Slots);

    public static bool operator ==(Room lhs, Room rhs) => lhs.Equals(rhs);

    public static bool operator !=(Room lhs, Room rhs) => !lhs.Equals(rhs);

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Type);

        foreach (var slot in Slots)
        {
            hashCode.Add(slot);
        }

        return hashCode.ToHashCode();
    }
}

public readonly record struct Tile(Pawn? Pawn)
{
    public static Tile Empty => new(null);

    public override string ToString() =>
        Pawn is null ? "." : Pawn.Type.ToString();
}

public class Pawn
{
    public PawnType Type { get; }

    public Pawn(PawnType type) => Type = type;

    public int MoveCost => Type switch
    {
        PawnType.A => 1,
        PawnType.B => 10,
        PawnType.C => 100,
        PawnType.D => 1000,
        _ => throw new InvalidOperationException()
    };
}

public enum PawnType
{
    A,
    B,
    C,
    D
}

