namespace Brimborium.DurableOrleans.Hosting.Test;
#if false
public class TodoGrainTests {
    [Fact]
    public async Task TestUsingTestClusterBuilder() {
        var builder = new TestClusterBuilder();
        //builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
        //Orleans.Runtime.KeyedServiceExtensions
        var cluster = builder.Build();
        cluster.Deploy();

        var primaryKey = Guid.NewGuid();
        var todoGrain = cluster.GrainFactory.GetGrain<ITodoGrain>(primaryKey);
        {
            var optionalResult = await todoGrain.GetAsync();
            Assert.False(optionalResult.TryGetValue(out var _));
        }
        var expected = new Todo(primaryKey, "dish", false);
        {
            var optionalResult = await todoGrain.SetAsync(expected);
            Assert.True(optionalResult.TryGetValue(out var actual));
            Assert.Equal("dish", actual.Name);
        }
        {
            var optionalResult = await todoGrain.GetAsync();
            Assert.True(optionalResult.TryGetValue(out var actual));
            Assert.Equal("dish", actual.Name);
        }

        cluster.StopAllSilos();

    }
}
#else

[Collection(ClusterCollection.Name)]
public class TodoGrainTests {
    private readonly TestCluster _Cluster;

    public TodoGrainTests(ClusterFixture fixture) {
        this._Cluster = fixture.Cluster;
    }

    [Fact]
    public async Task TodoGetSet() {
        var primaryKey = Guid.NewGuid();
        var todoGrain = this._Cluster.GrainFactory.GetGrain<ITodoGrain>(primaryKey);
        {
            var optionalResult = await todoGrain.GetAsync();
            Assert.False(optionalResult.TryGetValue(out var _));
        }
        var expected = new Todo(primaryKey, "dish", false);
        {
            var optionalResult = await todoGrain.SetAsync(expected);
            Assert.True(optionalResult.TryGetValue(out var actual));
            Assert.Equal(expected.Name, actual.Name);
        }
        {
            var optionalResult = await todoGrain.GetAsync();
            Assert.True(optionalResult.TryGetValue(out var actual));
            Assert.Equal(expected.Name, actual.Name);
        }
    }
}
#endif
