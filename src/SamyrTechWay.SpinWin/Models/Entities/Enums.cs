namespace SamyrTechWay.SpinWin.Models.Entities;

public enum WheelItemKind
{
    Prize = 1,
    TryAgain = 2,
}

public enum CelebrationTier
{
    Standard = 1,
    Rare = 2,
    Epic = 3,
}

public enum SpinResultKind
{
    Won = 1,
    TryAgain = 2,
}

public enum AwardStatus
{
    Pending = 1,
    Delivered = 2,
}

public enum CreditReason
{
    Initial = 1,
    AdminGrant = 2,
    AdminRevoke = 3,
    SpinDebit = 4,
    TryAgainRefund = 5,
}

public enum AnimationIntensity
{
    Subtle = 1,
    Standard = 2,
    Cinematic = 3,
}
