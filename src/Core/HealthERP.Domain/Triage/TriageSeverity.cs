namespace HealthERP.Domain.Triage;

public enum TriageSeverity
{
    Level1_Resuscitation = 1, // Immediate / Red
    Level2_Emergent = 2,      // < 15 mins / Orange
    Level3_Urgent = 3,        // < 30 mins / Yellow
    Level4_LessUrgent = 4,    // < 60 mins / Green
    Level5_NonUrgent = 5      // < 120 mins / Blue
}

public enum TriageStatus
{
    WaitingForTriage = 0,
    Triaged = 1,
    InBayCare = 2,
    TransferredToInpatient = 3,
    DischargedFromED = 4
}
