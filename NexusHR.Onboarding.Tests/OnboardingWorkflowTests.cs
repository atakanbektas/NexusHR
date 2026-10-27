using NexusHR.Onboarding.Domain.EmployeeOnboardings;

namespace NexusHR.Onboarding.Tests;

public sealed class OnboardingWorkflowTests
{
    internal static EmployeeOnboarding Create() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        "Ada", "Lovelace", "ada@example.test", "Engineer", "Engineering",
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), DateTime.UtcNow);

    [Fact]
    public void CompletedTasksMakeOnboardingReadyAndAllowCompletion()
    {
        var onboarding = Create();
        Assert.Equal(3, onboarding.Tasks.Count);
        Assert.Throws<InvalidOperationException>(() => onboarding.Complete(DateTime.UtcNow));
        foreach (var task in onboarding.Tasks)
        {
            onboarding.UpdateTask(task.Id, OnboardingTaskStatus.InProgress, DateTime.UtcNow);
            onboarding.UpdateTask(task.Id, OnboardingTaskStatus.Completed, DateTime.UtcNow);
            Assert.NotNull(task.CompletedAtUtc);
        }
        Assert.Equal(EmployeeOnboardingStatus.ReadyForStart, onboarding.Status);
        onboarding.Complete(DateTime.UtcNow);
        onboarding.Complete(DateTime.UtcNow);
        Assert.Equal(EmployeeOnboardingStatus.Completed, onboarding.Status);
        Assert.Throws<InvalidOperationException>(() => onboarding.UpdateTask(
            onboarding.Tasks.First().Id, OnboardingTaskStatus.InProgress, DateTime.UtcNow));
    }

    [Fact]
    public void TaskCompletionRequiresStartAndCannotBeReopened()
    {
        var onboarding = Create();
        var task = onboarding.Tasks.First();
        Assert.Throws<InvalidOperationException>(() => onboarding.UpdateTask(task.Id, OnboardingTaskStatus.Completed, DateTime.UtcNow));
        onboarding.UpdateTask(task.Id, OnboardingTaskStatus.InProgress, DateTime.UtcNow);
        onboarding.UpdateTask(task.Id, OnboardingTaskStatus.InProgress, DateTime.UtcNow);
        var completedAt = DateTime.UtcNow;
        onboarding.UpdateTask(task.Id, OnboardingTaskStatus.Completed, completedAt);
        onboarding.UpdateTask(task.Id, OnboardingTaskStatus.Completed, completedAt.AddMinutes(1));
        Assert.Equal(completedAt, task.CompletedAtUtc);
        Assert.Equal(EmployeeOnboardingStatus.Draft, onboarding.Status);
        Assert.Throws<InvalidOperationException>(() => onboarding.UpdateTask(task.Id, OnboardingTaskStatus.InProgress, DateTime.UtcNow));
    }

    [Fact]
    public void UnknownTasksInvalidStatesAndNonUtcTimesAreRejected()
    {
        var onboarding = Create();
        Assert.Throws<KeyNotFoundException>(() => onboarding.UpdateTask(Guid.NewGuid(), OnboardingTaskStatus.InProgress, DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => onboarding.UpdateTask(onboarding.Tasks.First().Id, OnboardingTaskStatus.Cancelled, DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => onboarding.Complete(DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified)));
    }
}
