using NewHarian.Application.Catalog;
using NewHarian.Domain.Enums;

namespace NewHarian.UnitTests.Application.Catalog;

[Trait("Category", "Unit")]
public class ServiceBookingStatusPolicyTests
{
    private static readonly HashSet<(ServiceBookingStatus From, ServiceBookingStatus To)> Allowed =
    [
        (ServiceBookingStatus.New, ServiceBookingStatus.Confirmed),
        (ServiceBookingStatus.Confirmed, ServiceBookingStatus.Completed),
        (ServiceBookingStatus.New, ServiceBookingStatus.Cancelled),
        (ServiceBookingStatus.Confirmed, ServiceBookingStatus.Cancelled),
    ];

    public static TheoryData<ServiceBookingStatus, ServiceBookingStatus, bool> TransitionMatrix()
    {
        var data = new TheoryData<ServiceBookingStatus, ServiceBookingStatus, bool>();
        foreach (var from in Enum.GetValues<ServiceBookingStatus>())
        foreach (var to in Enum.GetValues<ServiceBookingStatus>())
            data.Add(from, to, Allowed.Contains((from, to)));
        return data;
    }

    [Theory]
    [MemberData(nameof(TransitionMatrix))]
    public void CanTransition_matches_full_matrix(ServiceBookingStatus from, ServiceBookingStatus to, bool expected)
    {
        Assert.Equal(expected, ServiceBookingStatusPolicy.CanTransition(from, to));
    }

    [Fact]
    public void Cannot_skip_confirmation()
    {
        Assert.False(ServiceBookingStatusPolicy.CanTransition(ServiceBookingStatus.New, ServiceBookingStatus.Completed));
    }

    [Fact]
    public void CancellableStatuses_are_new_and_confirmed()
    {
        Assert.True(ServiceBookingStatusPolicy.CancellableStatuses.SetEquals(
            [ServiceBookingStatus.New, ServiceBookingStatus.Confirmed]));
    }
}
