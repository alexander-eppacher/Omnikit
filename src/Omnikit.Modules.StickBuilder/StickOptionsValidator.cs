using System.Text.RegularExpressions;
using Omnikit.Modules.Apps.Winget;
using Texts = Omnikit.Modules.StickBuilder.Resources.StickTexts;

namespace Omnikit.Modules.StickBuilder;

/// <summary>
/// Prüft die Eingaben, bevor eine Antwortdatei entsteht. Ein Fehler hier würde sonst erst mitten in der
/// Windows-Installation auffallen (z. B. ein Kontoname, den Windows nicht erlaubt).
/// </summary>
public static partial class StickOptionsValidator
{
    // Namen, die Windows für eigene Konten verwendet
    private static readonly string[] ReservedAccountNames = ["Administrator", "Guest", "Gast", "DefaultAccount", "WDAGUtilityAccount", "System"];

    /// <returns>Verständliche Fehlermeldungen; leer, wenn alles passt.</returns>
    public static IReadOnlyList<string> Validate(StickOptions options)
    {
        var errors = new List<string>();

        if (options.AccountName.Length == 0)
        {
            errors.Add(Texts.Error_AccountNameMissing);
        }
        else if (!AccountNamePattern().IsMatch(options.AccountName) || options.AccountName.EndsWith('.'))
        {
            errors.Add(Texts.Error_AccountName);
        }
        else if (ReservedAccountNames.Contains(options.AccountName, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add(Texts.Error_AccountNameReserved);
        }

        if (options.Password.Length > 127)
        {
            errors.Add(Texts.Error_PasswordTooLong);
        }

        if (options.ComputerName.Length > 0
            && (!ComputerNamePattern().IsMatch(options.ComputerName) || options.ComputerName.All(char.IsAsciiDigit)))
        {
            errors.Add(Texts.Error_ComputerName);
        }

        if (options.WifiName.Length > 0 && !WifiProfile.IsValidName(options.WifiName))
        {
            errors.Add(Texts.Error_WifiName);
        }
        else if (options.WifiName.Length > 0 && !WifiProfile.IsValidPassword(options.WifiPassword))
        {
            errors.Add(Texts.Error_WifiPassword);
        }

        if (!IsKnown(Choices.Languages, options.UiLanguage) || !IsKnown(Choices.Formats, options.UserLocale)
            || !IsKnown(Choices.Keyboards, options.InputLocale) || !IsKnown(Choices.TimeZones, options.TimeZone))
        {
            errors.Add(Texts.Error_Region);
        }

        if (!options.PackageIds.All(PackageId.IsValid) || !options.RemoveApps.All(app => IsKnown(Choices.RemovableApps, app)))
        {
            errors.Add(Texts.Error_Packages);
        }

        return errors;
    }

    private static bool IsKnown(IReadOnlyList<Choice> choices, string value) => choices.Any(choice => choice.Value == value);

    // Windows erlaubt 1–20 Zeichen ohne " / \ [ ] : ; | = , + * ? < > @ und ohne Steuerzeichen
    [GeneratedRegex(@"^[^""/\\\[\]:;|=,+*?<>@\p{C}]{1,20}$")]
    private static partial Regex AccountNamePattern();

    // NetBIOS-Name: 1–15 Zeichen, Buchstaben, Ziffern und Bindestrich, nicht am Anfang oder Ende
    [GeneratedRegex(@"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,13}[A-Za-z0-9])?$")]
    private static partial Regex ComputerNamePattern();
}
