namespace HealthERP.Application.Common.Security;

public static class Policies
{
    public const string CanViewPatients = "CanViewPatients";
    public const string CanEditPatients = "CanEditPatients";
    public const string CanPrescribeMedication = "CanPrescribeMedication";
    public const string CanDispenseMedication = "CanDispenseMedication";
    public const string CanOrderLabTest = "CanOrderLabTest";
    public const string CanEnterLabResult = "CanEnterLabResult";
    public const string CanManageAdmissions = "CanManageAdmissions";
    public const string CanManageBilling = "CanManageBilling";
    public const string CanManageStaff = "CanManageStaff";
    public const string CanViewExecutiveReports = "CanViewExecutiveReports";
}
