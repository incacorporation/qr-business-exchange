namespace QRBusinessExchange.Client.Components.Scanner;

public enum QrScanOutcomeKind
{
    /// <summary>The page is navigating away (e.g. to the scanned link).</summary>
    Opening,

    /// <summary>Done; show a confirmation and a "Scan another" button.</summary>
    Success,

    /// <summary>The code can't be used here; show why and a "Scan again" button.</summary>
    Warning
}

/// <summary>What <see cref="QrScanner"/> shows after its host handled a scanned code.</summary>
public sealed record QrScanOutcome(QrScanOutcomeKind Kind, string? Title = null, string? Detail = null, bool ShowScannedText = false)
{
    public static QrScanOutcome Opening() => new(QrScanOutcomeKind.Opening);

    public static QrScanOutcome Success(string title, string? detail = null) => new(QrScanOutcomeKind.Success, title, detail);

    /// <param name="showScannedText">Also show the code's text (helps when it isn't a qrbound code at all).</param>
    public static QrScanOutcome Warning(string title, string detail, bool showScannedText = false) =>
        new(QrScanOutcomeKind.Warning, title, detail, showScannedText);
}
