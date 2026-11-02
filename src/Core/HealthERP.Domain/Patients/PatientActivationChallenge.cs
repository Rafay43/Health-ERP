using System.Security.Cryptography;
using System.Text;
using HealthERP.SharedKernel;

namespace HealthERP.Domain.Patients;

public class PatientActivationChallenge : BaseEntity<Guid>, IAuditable
{
    public Guid UserId { get; private set; }
    public PatientId PatientId { get; private set; } = default!;
    public string CodeHash { get; private set; } = default!;
    public DateTime ExpiresAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public bool IsConsumed { get; private set; }
    public DateTime? ResendCooldownUntilUtc { get; private set; }

    // IAuditable
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    private PatientActivationChallenge() { }

    private PatientActivationChallenge(
        Guid id,
        Guid userId,
        PatientId patientId,
        string codeHash,
        DateTime expiresAtUtc,
        DateTime? resendCooldownUntilUtc)
    {
        Id = id;
        UserId = userId;
        PatientId = patientId;
        CodeHash = codeHash;
        ExpiresAtUtc = expiresAtUtc;
        AttemptCount = 0;
        IsConsumed = false;
        ResendCooldownUntilUtc = resendCooldownUntilUtc;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static PatientActivationChallenge Create(
        Guid userId,
        PatientId patientId,
        string rawCode,
        TimeSpan validFor,
        TimeSpan cooldown)
    {
        var codeHash = HashCode(rawCode);
        var now = DateTime.UtcNow;
        return new PatientActivationChallenge(
            Guid.NewGuid(),
            userId,
            patientId,
            codeHash,
            now.Add(validFor),
            now.Add(cooldown));
    }

    public bool VerifyCode(string inputCode)
    {
        if (IsConsumed || DateTime.UtcNow > ExpiresAtUtc || AttemptCount >= 5)
        {
            return false;
        }

        AttemptCount++;

        var inputHash = HashCode(inputCode);
        var inputHashBytes = Encoding.UTF8.GetBytes(inputHash);
        var storedHashBytes = Encoding.UTF8.GetBytes(CodeHash);

        var isMatch = CryptographicOperations.FixedTimeEquals(inputHashBytes, storedHashBytes);

        if (isMatch || AttemptCount >= 5)
        {
            IsConsumed = true;
        }

        return isMatch;
    }

    public void MarkConsumed()
    {
        IsConsumed = true;
    }

    public static string HashCode(string code)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(code.Trim() + "_HealthERP_Activation_Salt_2026");
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hashBytes);
    }
}
