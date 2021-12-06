using System.Collections.Immutable;
using Common;
using Superpower;
using Superpower.Parsers;

new Runner().Run(
    Numerics.IntegerInt32.ManyDelimitedBy(Character.EqualTo(',')),
    values =>
    {
        var initialState = new State(values, 7, 2);
        var finalState = initialState.AdvanceBy(80);
        return finalState.SpecimenCountByTimer.Sum();
    }, 
    values =>
    {
        var initialState = new State(values, 7, 2);
        var finalState = initialState.AdvanceBy(256);
        return finalState.SpecimenCountByTimer.Sum();
    });

public readonly record struct State
{
    private readonly int _timerCycle;

    private readonly int _newSpecimenOffset;

    public ImmutableArray<long> SpecimenCountByTimer { get; private init; }

    public int TicksPased { get; private init; }

    public State(int[] initialSpecimen, int timerCycle, int newSpecimenOffset)
    {
        var builder = ImmutableArray.CreateBuilder<long>(timerCycle + newSpecimenOffset);
        builder.Count = timerCycle + newSpecimenOffset;

        foreach (var specimen in initialSpecimen)
        {
            builder[specimen] += 1;
        }

        _timerCycle = timerCycle;
        _newSpecimenOffset = newSpecimenOffset;
        SpecimenCountByTimer = builder.ToImmutable();
        TicksPased = 0;
    }

    public State NextTick()
    {
        var builder = ImmutableArray.CreateBuilder<long>(SpecimenCountByTimer.Length);
        builder.Count = SpecimenCountByTimer.Length;

        for (var i = 1; i < SpecimenCountByTimer.Length; i += 1)
        {
            builder[i - 1] = SpecimenCountByTimer[i];
        }

        builder[_timerCycle - 1] += SpecimenCountByTimer[0];
        builder[_timerCycle + _newSpecimenOffset - 1] += SpecimenCountByTimer[0];

        return this with { SpecimenCountByTimer = builder.ToImmutable(), TicksPased = TicksPased + 1 };
    }

    public State AdvanceBy(int ticks) =>
        Enumerable.Range(0, ticks).Aggregate(this, (s, _) => s.NextTick());
}