namespace NexusHR.BuildingBlocks.Security;

public static class NexusHrRoles
{
    public const string HrSpecialist =
        "HrSpecialist";

    public const string HrManager =
        "HrManager";

    public const string DepartmentManager =
        "DepartmentManager";

    public const string ItSpecialist =
        "ItSpecialist";

    public const string FinanceSpecialist =
        "FinanceSpecialist";

    public const string Admin =
        "Admin";

    public const string Auditor =
        "Auditor";

    public const string CandidateReaders =
        "HrSpecialist,HrManager,DepartmentManager,Auditor";

    public const string CandidateEditors =
        "HrSpecialist";
}