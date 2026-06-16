using SadcOMS.Worker.Services;

namespace SadcOMS.Tests.Worker;

public class MessageRetryPolicyTests
{
  [Theory]
  [InlineData(null, 0)]
  [InlineData(0, 0)]
  [InlineData(1, 1)]
  [InlineData(2, 2)]
  public void GetRetryCount_ReadsHeaderOrDefaults(int? headerValue, int expected)
  {
    IDictionary<string, object>? headers = headerValue is null
      ? null
      : new Dictionary<string, object> { ["x-retry-count"] = headerValue.Value };

    Assert.Equal(expected, MessageRetryPolicy.GetRetryCount(headers));
  }

  [Theory]
  [InlineData(0, false)]
  [InlineData(1, false)]
  [InlineData(2, false)]
  [InlineData(3, true)]
  [InlineData(4, true)]
  public void ShouldDeadLetter_AfterMaxRetries_RoutesToDlq(int retryCount, bool expectDeadLetter)
  {
    Assert.Equal(expectDeadLetter, MessageRetryPolicy.ShouldDeadLetter(retryCount));
  }

  [Fact]
  public void FourthFailureAttempt_HasRetryCountThree_AndDeadLetters()
  {
    // Simulates failures at republished counts 1, 2, 3, then DLQ on the fourth delivery (count 3).
    var retryCount = 0;
    for (var attempt = 1; attempt <= 4; attempt++)
    {
      if (MessageRetryPolicy.ShouldDeadLetter(retryCount))
      {
        Assert.Equal(4, attempt);
        Assert.Equal(3, retryCount);
        return;
      }

      retryCount = MessageRetryPolicy.NextRetryCount(retryCount);
    }

    Assert.Fail("Expected dead-letter on the fourth processing attempt.");
  }
}
