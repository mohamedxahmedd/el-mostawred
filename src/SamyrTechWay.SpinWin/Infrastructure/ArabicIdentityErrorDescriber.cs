using Microsoft.AspNetCore.Identity;

namespace SamyrTechWay.SpinWin.Infrastructure;

/// <summary>Arabic Identity errors: the admin area is Arabic-only and these messages surface on the change-password form.</summary>
public sealed class ArabicIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() =>
        new() { Code = nameof(DefaultError), Description = "حدث خطأ غير متوقع. حاول مرة أخرى." };

    public override IdentityError ConcurrencyFailure() =>
        new() { Code = nameof(ConcurrencyFailure), Description = "تم تعديل هذا السجل من مستخدم آخر. حدّث الصفحة وحاول مرة أخرى." };

    public override IdentityError PasswordMismatch() =>
        new() { Code = nameof(PasswordMismatch), Description = "كلمة المرور الحالية غير صحيحة." };

    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"يجب ألا تقل كلمة المرور عن {length} حرفًا." };

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        new() { Code = nameof(PasswordRequiresUniqueChars), Description = $"يجب أن تحتوي كلمة المرور على {uniqueChars} أحرف مختلفة على الأقل." };

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "يجب أن تحتوي كلمة المرور على رمز واحد على الأقل (مثل ! أو @)." };

    public override IdentityError PasswordRequiresDigit() =>
        new() { Code = nameof(PasswordRequiresDigit), Description = "يجب أن تحتوي كلمة المرور على رقم واحد على الأقل (0-9)." };

    public override IdentityError PasswordRequiresLower() =>
        new() { Code = nameof(PasswordRequiresLower), Description = "يجب أن تحتوي كلمة المرور على حرف إنجليزي صغير واحد على الأقل (a-z)." };

    public override IdentityError PasswordRequiresUpper() =>
        new() { Code = nameof(PasswordRequiresUpper), Description = "يجب أن تحتوي كلمة المرور على حرف إنجليزي كبير واحد على الأقل (A-Z)." };

    public override IdentityError InvalidEmail(string? email) =>
        new() { Code = nameof(InvalidEmail), Description = "البريد الإلكتروني غير صحيح." };

    public override IdentityError DuplicateEmail(string email) =>
        new() { Code = nameof(DuplicateEmail), Description = "هذا البريد الإلكتروني مستخدم بالفعل." };

    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = "اسم المستخدم غير صحيح. استخدم حروفًا وأرقامًا إنجليزية فقط." };

    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = "اسم المستخدم مستخدم بالفعل." };
}
