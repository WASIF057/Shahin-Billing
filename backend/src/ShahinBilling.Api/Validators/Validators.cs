using FluentValidation;
using ShahinBilling.Api.Dtos;
using ShahinBilling.Api.Helpers;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Validators;

internal static class Rules
{
    public const int MaxLogoChars = 700_000;       // ~500 KB image as base64
    public const int MaxSignatureChars = 420_000;  // ~300 KB image as base64

    public static IRuleBuilderOptions<T, string> OptionalGstin<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(g => string.IsNullOrWhiteSpace(g) || GstinValidator.IsValid(g))
            .WithMessage("GSTIN is not valid. Check the 15 characters (for example 27ABCDE1234F1Z5).");

    public static IRuleBuilderOptions<T, string> OptionalImage<T>(this IRuleBuilder<T, string> rule, int maxChars, string label) =>
        rule.Must(s => string.IsNullOrEmpty(s) ||
                       (s.Length <= maxChars && (s.StartsWith("data:image/png") || s.StartsWith("data:image/jpeg") || s.StartsWith("data:image/jpg"))))
            .WithMessage($"{label} must be a PNG or JPG image under {maxChars * 3 / 4 / 1024} KB.");

    public static IRuleBuilderOptions<T, string> OptionalStateCode<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(c => string.IsNullOrEmpty(c) || StateCodes.All.ContainsKey(c)).WithMessage("Select a valid state.");
}

public class RegisterValidator : AbstractValidator<RegisterRequest>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Enter your name.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Enter a valid email address.");
        RuleFor(x => x.Password).MinimumLength(8).WithMessage("Password must be at least 8 characters.");
        RuleFor(x => x.BusinessName).NotEmpty().WithMessage("Enter your business name.");
    }
}

public class ResetPasswordValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Enter your email address.");
        RuleFor(x => x.Code).Matches("^[0-9]{6}$").WithMessage("Enter the 6-digit code from the email.");
        RuleFor(x => x.NewPassword).MinimumLength(8).WithMessage("Password must be at least 8 characters.");
    }
}

public class CreateStaffValidator : AbstractValidator<CreateStaffRequest>
{
    public CreateStaffValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Enter the staff member's name.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Enter a valid email address.");
        RuleFor(x => x.Password).MinimumLength(8).WithMessage("Password must be at least 8 characters.");
    }
}

public class ChangePasswordValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).MinimumLength(8).WithMessage("New password must be at least 8 characters.");
    }
}

public class BusinessValidator : AbstractValidator<Business>
{
    public BusinessValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Enter the business name.");
        RuleFor(x => x.Gstin).OptionalGstin();
        RuleFor(x => x.Address.StateCode).OptionalStateCode();
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("Enter a valid email address.");
        RuleFor(x => x.Logo).OptionalImage(Rules.MaxLogoChars, "Logo");
        RuleFor(x => x.Signature).OptionalImage(Rules.MaxSignatureChars, "Signature");
        RuleFor(x => x.Bank.Ifsc).Matches("^[A-Z]{4}0[A-Z0-9]{6}$").When(x => !string.IsNullOrWhiteSpace(x.Bank.Ifsc))
            .WithMessage("IFSC should look like HDFC0001234.");
        RuleFor(x => x.InvoiceNumbering.Padding).InclusiveBetween(1, 8);
        RuleFor(x => x.InvoiceNumbering.Prefix).MaximumLength(10);
        RuleFor(x => x.InvoiceNumbering.NonGstPrefix).NotEmpty().MaximumLength(10).WithMessage("Enter a prefix for Non-GST bills (for example NG).");
        RuleFor(x => x.InvoiceNumbering).Must(n => !string.Equals((n.NonGstPrefix ?? "").Trim(), (n.Prefix ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
            .WithMessage("The Non-GST bill prefix must be different from the GST invoice prefix, so bill numbers never clash.");
        RuleFor(x => x.InvoiceNumbering.Separator).MaximumLength(2);
    }
}

public class TemplateValidator : AbstractValidator<TemplateSettings>
{
    public TemplateValidator()
    {
        RuleFor(x => x.Layout).Must(l => l is "Classic" or "Modern").WithMessage("Choose Classic or Modern.");
        RuleFor(x => x.FontSize).Must(f => f is "Small" or "Normal" or "Large");
        RuleFor(x => x.PrimaryColor).Matches("^#[0-9A-Fa-f]{6}$").WithMessage("Pick a colour.");
        RuleFor(x => x.InvoiceTitle).NotEmpty().MaximumLength(40);
    }
}

public class ReminderSettingsValidator : AbstractValidator<ReminderSettings>
{
    public ReminderSettingsValidator()
    {
        RuleFor(x => x.AfterDays).InclusiveBetween(1, 365).WithMessage("Choose between 1 and 365 days.");
        RuleFor(x => x.RepeatEveryDays).InclusiveBetween(1, 90).WithMessage("Repeat every 1 to 90 days.");
        RuleFor(x => x.MaxReminders).InclusiveBetween(1, 10).WithMessage("Send between 1 and 10 reminders.");
    }
}

public class EmailTemplateValidator : AbstractValidator<EmailTemplate>
{
    public EmailTemplateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(60).WithMessage("Give the template a name.");
        RuleFor(x => x.Recipient).Must(r => r is EmailTemplate.ToClient or EmailTemplate.ToBusiness)
            .WithMessage("Choose who receives it: the client or you.");
        RuleFor(x => x.Triggers).NotEmpty().WithMessage("Choose when this email should be sent.");
        RuleForEach(x => x.Triggers).Must(t => t is EmailTemplate.OnGenerated or EmailTemplate.OnDownloaded or EmailTemplate.OnPaymentChanged or EmailTemplate.OnReminder or EmailTemplate.OnOrderPlaced or EmailTemplate.OnOrderAccepted or EmailTemplate.OnOrderCancelled)
            .WithMessage("Unknown trigger.");
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200).WithMessage("Enter the email subject.");
        RuleFor(x => x.Body).NotEmpty().MaximumLength(5000).WithMessage("Enter the email message.");
    }
}

public class ProductTypeValidator : AbstractValidator<ProductType>
{
    public ProductTypeValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(40).WithMessage("Enter the type name (for example Bed or Pillow).");
        RuleFor(x => x.Sizes).Must(l => l == null || l.Count <= 200).WithMessage("Too many sizes.");
        RuleFor(x => x.Variants).Must(l => l == null || l.Count <= 200).WithMessage("Too many variants.");
        RuleFor(x => x.ClothTypes).Must(l => l == null || l.Count <= 50).WithMessage("Too many cloth types.");
        RuleForEach(x => x.ClothTypes).MaximumLength(60);
        RuleForEach(x => x.ClothColours).ChildRules(c =>
        {
            c.RuleFor(s => s.Colours).Must(l => l == null || l.Count <= 100).WithMessage("Too many colours for one cloth.");
            c.RuleForEach(s => s.Colours).MaximumLength(60);
        });
        RuleForEach(x => x.Sizes).MaximumLength(60);
        RuleForEach(x => x.Variants).MaximumLength(60);
    }
}

public class BillFormatValidator : AbstractValidator<BillFormat>
{
    public BillFormatValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(60).WithMessage("Give the format a name.");
        RuleFor(x => x.Settings).NotNull().SetValidator(new TemplateValidator());
    }
}

public class ItemValidator : AbstractValidator<Item>
{
    public ItemValidator()
    {
        RuleFor(x => x.Name).NotEmpty().When(x => string.IsNullOrWhiteSpace(x.TypeId)).WithMessage("Enter the item name.");
        RuleFor(x => x.HsnCode).Matches("^[0-9]{4,8}$").When(x => !string.IsNullOrWhiteSpace(x.HsnCode))
            .WithMessage("HSN code should be 4 to 8 digits.");
        RuleFor(x => x.Unit).NotEmpty();
        RuleFor(x => x.GstRate).InclusiveBetween(0, 28).WithMessage("GST rate must be between 0 and 28%.");
        RuleFor(x => x.DefaultRate).GreaterThanOrEqualTo(0).WithMessage("Rate can't be negative.");
        RuleForEach(x => x.SpecialRates).ChildRules(s =>
        {
            s.RuleFor(r => r.Rate).GreaterThanOrEqualTo(0).WithMessage("Special rate can't be negative.");
        });
    }
}

public class ClientValidator : AbstractValidator<Client>
{
    public ClientValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Enter the client name.");
        RuleFor(x => x.Gstin).OptionalGstin();
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("Enter a valid email address.");
        RuleFor(x => x.Phone).Matches(@"^[0-9+\- ]{7,15}$").When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Enter a valid mobile number (digits only, 7 to 15 characters).");
        RuleFor(x => x.BillingAddress.StateCode).NotEmpty().WithMessage("Select the billing state (needed for GST).")
            .OptionalStateCode();
        RuleFor(x => x.ShippingAddress.StateCode).OptionalStateCode().When(x => !x.ShippingSameAsBilling);
    }
}

public class InvoiceRequestValidator : AbstractValidator<InvoiceRequest>
{
    public InvoiceRequestValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty().WithMessage("Select a client.");
        RuleFor(x => x.InvoiceDate).NotEmpty().WithMessage("Select the invoice date.");
        RuleFor(x => x.PlaceOfSupplyStateCode).OptionalStateCode();
        RuleFor(x => x.Lines).NotEmpty().When(x => x.Finalize).WithMessage("Add at least one item before finalizing.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(i => i.ItemId).NotEmpty().WithMessage("Select an item on every row.");
            l.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Quantity must be more than 0.");
            l.RuleFor(i => i.Rate).GreaterThanOrEqualTo(0).When(i => i.Rate.HasValue).WithMessage("Rate can't be negative.");
            l.RuleFor(i => i.Discount).GreaterThanOrEqualTo(0).WithMessage("Discount can't be negative.");
            l.RuleFor(i => i).Must(i => !i.Rate.HasValue || i.Discount <= i.Quantity * i.Rate.Value)
                .WithMessage("Discount can't be more than the line amount.");
        });
    }
}

public class PaymentValidator : AbstractValidator<PaymentRequest>
{
    public PaymentValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Enter the amount received.");
        RuleFor(x => x.Mode).Must(m => m is "Cash" or "Bank Transfer" or "UPI" or "Cheque").WithMessage("Choose a payment mode.");
    }
}
