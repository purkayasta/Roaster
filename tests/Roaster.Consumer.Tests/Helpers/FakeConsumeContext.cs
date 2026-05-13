using MassTransit;
using Moq;

namespace Roaster.Consumer.Tests.Helpers;

public static class FakeConsumeContext
{
    public static ConsumeContext<T> For<T>(T message) where T : class
    {
        var mock = new Mock<ConsumeContext<T>>();
        mock.Setup(c => c.Message).Returns(message);
        mock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }
}
