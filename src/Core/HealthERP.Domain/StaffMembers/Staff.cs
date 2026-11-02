using HealthERP.SharedKernel;

namespace HealthERP.Domain.StaffMembers;

public class StaffMembers : BaseEntity<StaffId>, IAuditable
{
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string PhoneNumber { get; private set; } = default!;
    public string Specialty { get; private set; } = default!;
    public string LicenseNumber { get; private set; } = default!;
    public string Department { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;
    public bool IsDeleted { get; private set; } = false;
    public Guid? UserId { get; private set; }
    public StaffKind StaffKind { get; private set; } = StaffKind.Unspecified;

    // IAuditable
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    private StaffMembers() { } // EF Core

    private StaffMembers(
        StaffId id,
        string firstName,
        string lastName,
        string email,
        string phoneNumber,
        string specialty,
        string licenseNumber,
        string department)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        PhoneNumber = phoneNumber;
        Specialty = specialty;
        LicenseNumber = licenseNumber;
        Department = department;
    }

    public static StaffMembers Create(
        string firstName,
        string lastName,
        string email,
        string phoneNumber,
        string specialty,
        string licenseNumber,
        string department)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required.", nameof(firstName));

        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name is required.", nameof(lastName));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        if (string.IsNullOrWhiteSpace(specialty))
            throw new ArgumentException("Specialty is required.", nameof(specialty));

        return new StaffMembers(
            StaffId.New(),
            firstName,
            lastName,
            email,
            phoneNumber,
            specialty,
            licenseNumber,
            department);
    }

    public void UpdateProfile(string firstName, string lastName, string phoneNumber, string specialty, string department)
    {
        FirstName = firstName;
        LastName = lastName;
        PhoneNumber = phoneNumber;
        Specialty = specialty;
        Department = department;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;

    public void LinkUserId(Guid userId)
    {
        if (userId == Guid.Empty) throw new ArgumentException("Identity account is required.", nameof(userId));
        if (UserId.HasValue && UserId != userId)
            throw new InvalidOperationException("Staff is already linked to an Identity account.");
        UserId = userId;
    }

    public void UnlinkUserId() => UserId = null;

    public void AssignStaffKind(StaffKind kind)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind), "Invalid staff classification.");
        StaffKind = kind;
    }
}
