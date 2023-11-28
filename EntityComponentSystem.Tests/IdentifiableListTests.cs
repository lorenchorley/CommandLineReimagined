using Moq;
using UIComponents.Components;

namespace EntityComponentSystem.Tests;

[TestClass]
public class IdentifiableListTests
{
    [TestMethod]
    public void AddToECSTests()
    {
        // Arrange
        // =======
        Mock<IServiceProvider> sp = new Mock<IServiceProvider>();
        ECS ecs = new ECS(sp.Object);

        // Act
        // ===
        ecs.NewEntity("1");
        ecs.NewEntity("2");
        ecs.NewEntity("3");

        int entityCount1 = ecs.Entities.Count();
        int componentCount1 = ecs.Components.Count();

        Entity e = ecs.NewEntity("4");

        e.AddComponent<ButtonComponent>();
        e.AddComponent<TextComponent>();

        // TODO 2 components
        ecs.NewEntity("5");

        int entityCount2 = ecs.Entities.Count();
        int componentCount2 = ecs.Components.Count();

        e.AddComponent<PathInformation>();

        int entityCount3 = ecs.Entities.Count();
        int componentCount3 = ecs.Components.Count();


        // Assert
        // ======
        Assert.AreEqual(4, entityCount1);
        Assert.AreEqual(0, componentCount1);
        
        Assert.AreEqual(6, entityCount2);
        Assert.AreEqual(5, componentCount2);
        
        Assert.AreEqual(6, entityCount3);
        Assert.AreEqual(6, componentCount3);


    }

    public class IdObj : IIdentifiable
    {
        public int Id { get; init; }
    }

    [TestMethod]
    public void AddToIsolatedListTests()
    {
        // Arrange
        // =======
        IdentifiableList list = new IdentifiableList();
        int i = 0;

        // Act
        // ===
        list.Set(new IdObj() { Id = i++ });

        int count1 = list.Count<IdObj>();

        var e = new IdObj() { Id = i++ };

        list.Set(new IdObj() { Id = i++ });

        int count2 = list.Count<IdObj>();

        list.Set(e);

        int count3 = list.Count<IdObj>();


        // Assert
        // ======
        Assert.AreEqual(1, count1);
        Assert.AreEqual(2, count2);
        Assert.AreEqual(3, count3);


    }
}