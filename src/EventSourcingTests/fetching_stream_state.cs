using System;
using System.Threading.Tasks;
using EventSourcingTests.Projections;
using Marten;
using Marten.Testing.Harness;
using Shouldly;
using Xunit;

namespace EventSourcingTests;

public class fetching_stream_state_before_aggregator_is_registered: IntegrationContext
{
    [Fact]
    public async Task bug_705_order_of_operation()
    {
        var streamId = Guid.NewGuid();

        using (var session = theStore.OpenSession())
        {
            var joined = new MembersJoined { Members = new string[] { "Rand", "Matt", "Perrin", "Thom" } };
            var departed = new MembersDeparted { Members = new[] { "Thom" } };

            session.Events.StartStream<QuestParty>(streamId, joined, departed);
            await session.SaveChangesAsync();
        }

        using (var query = theStore.OpenSession())
        {
            var state = await query.Events.FetchStreamStateAsync(streamId);
            var aggregate = await query.Events.AggregateStreamAsync<QuestParty>(streamId);

            state.ShouldNotBeNull();
            aggregate.ShouldNotBeNull();
        }
    }

    [Fact]
    public void other_try()
    {
        var store = DocumentStore.For(_ =>
        {
            _.Connection(ConnectionSource.ConnectionString);
            _.Events.AddEventTypes(new[] { typeof(FooEvent), });
        });

        using (var session = store.OpenSession())
        {
            var aid = Guid.Parse("1442cbbb-a49a-497e-9ee8-715ed2833bf8");
            session.Events.StartStream<FooAggregate>(aid, new FooEvent());
            session.SaveChanges();
        }

        var store2 = DocumentStore.For(_ =>
        {
            _.Connection(ConnectionSource.ConnectionString);
            _.Events.AddEventTypes(new[] { typeof(FooEvent), });

            _.Projections.AggregatorFor<FooAggregate>();
        });

        using (var session = store2.OpenSession())
        {
            var aid = Guid.Parse("1442cbbb-a49a-497e-9ee8-715ed2833bf8");
            var state = session.Events.FetchStreamState(aid);
            // We never get to the AggregateStream call because we get a nullreference exception on the FetchStreamState call
            var aggregate = session.Events.AggregateStream<FooAggregate>(aid);
        }
    }

    public fetching_stream_state_before_aggregator_is_registered(DefaultStoreFixture fixture) : base(fixture)
    {
    }
}

public class FooEvent { }

public class FooAggregate
{
    public Guid Id;

    public void Apply(FooEvent e){}
}

#region sample_fetching_stream_state
public class fetching_stream_state: IntegrationContext
{
    private Guid theStreamId;

    public fetching_stream_state(DefaultStoreFixture fixture) : base(fixture)
    {

    }

    protected override Task fixtureSetup()
    {
        var joined = new MembersJoined { Members = new string[] { "Rand", "Matt", "Perrin", "Thom" } };
        var departed = new MembersDeparted { Members = new[] { "Thom" } };

        theStreamId = theSession.Events.StartStream<Quest>(joined, departed).Id;
        return theSession.SaveChangesAsync();
    }

    [Fact]
    public void can_fetch_the_stream_version_and_aggregate_type()
    {
        var state = theSession.Events.FetchStreamState(theStreamId);

        state.Id.ShouldBe(theStreamId);
        state.Version.ShouldBe(2);
        state.AggregateType.ShouldBe(typeof(Quest));
        state.LastTimestamp.ShouldNotBe(DateTime.MinValue);
        state.Created.ShouldNotBe(DateTime.MinValue);
    }

    [Fact]
    public async Task can_fetch_the_stream_version_and_aggregate_type_async()
    {
        var state = await theSession.Events.FetchStreamStateAsync(theStreamId);

        state.Id.ShouldBe(theStreamId);
        state.Version.ShouldBe(2);
        state.AggregateType.ShouldBe(typeof(Quest));
        state.LastTimestamp.ShouldNotBe(DateTime.MinValue);
        state.Created.ShouldNotBe(DateTime.MinValue);
    }

    [Fact]
    public async Task can_fetch_the_stream_version_through_batch_query()
    {
        var batch = theSession.CreateBatchQuery();

        var stateTask = batch.Events.FetchStreamState(theStreamId);

        await batch.Execute();

        var state = await stateTask;

        state.Id.ShouldBe(theStreamId);
        state.Version.ShouldBe(2);
        state.AggregateType.ShouldBe(typeof(Quest));
        state.LastTimestamp.ShouldNotBe(DateTime.MinValue);
    }

    [Fact]
    public async Task can_fetch_the_stream_events_through_batch_query()
    {
        var batch = theSession.CreateBatchQuery();

        var eventsTask = batch.Events.FetchStream(theStreamId);

        await batch.Execute();

        var events = await eventsTask;

        events.Count.ShouldBe(2);
    }
}

#endregion