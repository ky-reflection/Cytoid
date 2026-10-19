using System.Reflection;
using NUnit.Framework;

public class TimelineDragStackResyncTests
{
    [Test]
    public void TimelineDespawnRemovesStackMembersBeforeTheirModelsAreCleared()
    {
        using (var fixture = new DropDragGuardTests.InputFixture(GameMode.Practice))
        {
            var first = fixture.DragChild(1, 10);
            var second = fixture.DragChild(2, 10);
            var host = new DragStackHost(7);
            host.Add(first);
            host.Add(second);
            Assert.That(second.IsDragStackFollower, Is.True);

            Despawn(first);
            Assert.That(host.Members, Is.EquivalentTo(new[] {second}));
            Assert.That(first.Model, Is.Null);
            Assert.That(first.DragStack, Is.Null);
            Assert.That(host.Primary, Is.SameAs(second));
            Assert.That(second.IsDragStackFollower, Is.False);

            Despawn(second);
            Assert.That(host.Members, Is.Empty);
            Assert.That(host.Primary, Is.Null);

            // The next timeline spawn must not compare against a pooled note's null Model.
            var replacement = fixture.DragChild(3, 10);
            Assert.DoesNotThrow(() => host.Add(replacement));
            Assert.That(host.Primary, Is.SameAs(replacement));
        }
    }

    private static void Despawn(Note note)
    {
        typeof(Note).GetMethod("ForceDespawnForResync", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(note, null);
    }
}
