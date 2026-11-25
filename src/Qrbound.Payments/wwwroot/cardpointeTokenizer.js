// Relays what CardPointe's hosted iFrame tokenizer posts to the page back to .NET.
// Only messages from that iframe's window and origin are accepted.
export function listen(iframe, expectedOrigin, dotnetRef) {
    const handler = (event) => {
        if (event.origin !== expectedOrigin || event.source !== iframe.contentWindow) {
            return;
        }

        let data = event.data;
        if (typeof data === "string") {
            try {
                data = JSON.parse(data);
            } catch {
                return;
            }
        }

        if (!data || typeof data !== "object") {
            return;
        }

        dotnetRef.invokeMethodAsync(
            "OnTokenizerMessage",
            typeof data.message === "string" ? data.message : null,
            typeof data.expiry === "string" ? data.expiry : null,
            typeof data.validationError === "string" ? data.validationError : null);
    };

    window.addEventListener("message", handler);

    return {
        dispose: () => window.removeEventListener("message", handler)
    };
}
