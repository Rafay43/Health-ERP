using HealthERP.Domain.StaffMembers;
using Staff = HealthERP.Domain.StaffMembers.StaffMembers;

namespace HealthERP.Domain.UnitTests.StaffMembers;

public class StaffAssociationTests
{
    private static Staff Create() => Staff.Create("Test", "Staff", "staff@example.invalid", "", "General", "", "Outpatient");

    [Fact] public void NewStaff_IsUnlinkedAndUnspecified()
    { var staff = Create(); Assert.Null(staff.UserId); Assert.Equal(StaffKind.Unspecified, staff.StaffKind); }

    [Fact] public void ValidAccountCanBeLinked()
    { var staff = Create(); var id = Guid.NewGuid(); staff.LinkUserId(id); Assert.Equal(id, staff.UserId); }

    [Fact] public void EmptyAccountCannotBeLinked()
    { var staff = Create(); Assert.Throws<ArgumentException>(() => staff.LinkUserId(Guid.Empty)); Assert.Null(staff.UserId); }

    [Fact] public void DifferentAccountCannotOverwriteAssociation()
    {
        var staff = Create(); var id = Guid.NewGuid(); staff.LinkUserId(id);
        Assert.Throws<InvalidOperationException>(() => staff.LinkUserId(Guid.NewGuid())); Assert.Equal(id, staff.UserId);
    }

    [Fact] public void ExactSameAssociationIsIdempotent()
    { var staff = Create(); var id = Guid.NewGuid(); staff.LinkUserId(id); staff.LinkUserId(id); Assert.Equal(id, staff.UserId); }

    [Fact] public void ProfileAndClassificationChangesPreserveAssociation()
    {
        var staff = Create(); var id = Guid.NewGuid(); staff.LinkUserId(id);
        staff.UpdateProfile("Changed", "Name", "123", "Surgery", "Ward"); staff.AssignStaffKind(StaffKind.Doctor);
        Assert.Equal(id, staff.UserId); Assert.Equal(StaffKind.Doctor, staff.StaffKind);
    }

    [Fact] public void ExplicitUnlinkIsIdempotentAndAllowsDeliberateNewAssociation()
    {
        var staff = Create(); staff.LinkUserId(Guid.NewGuid()); staff.UnlinkUserId(); staff.UnlinkUserId();
        Assert.Null(staff.UserId); var id = Guid.NewGuid(); staff.LinkUserId(id); Assert.Equal(id, staff.UserId);
    }

    [Fact] public void ClassificationIsExplicitAndCanReturnToUnspecified()
    {
        var staff = Create(); staff.AssignStaffKind(StaffKind.Doctor); Assert.Equal(StaffKind.Doctor, staff.StaffKind);
        staff.AssignStaffKind(StaffKind.Unspecified); Assert.Equal(StaffKind.Unspecified, staff.StaffKind);
    }

    [Fact] public void InvalidClassificationIsRejected()
    { var staff = Create(); Assert.Throws<ArgumentOutOfRangeException>(() => staff.AssignStaffKind((StaffKind)42)); Assert.Equal(StaffKind.Unspecified, staff.StaffKind); }
}
