namespace InventoryManagement.Web.Services
{
    /// <summary>Picks a Font Awesome icon for a product without a photo from words in its category name.</summary>
    public static class CategoryIcons
    {
        public const string Default = "fa-box";

        /// <summary>Words looked for in the lower-case category name, the first match winning, so the narrower words come first.</summary>
        public static readonly IReadOnlyList<(string[] Words, string Icon)> Rules =
        [
            (["finger", "barmaq"], "fa-fingerprint"),
            (["barcode", "barkod"], "fa-barcode"),
            (["label", "etiket"], "fa-tag"),
            (["thermo", "miniprinter", "çek"], "fa-receipt"),
            (["printer", "scanner", "skaner"], "fa-print"),
            (["notebook", "laptop", "noutbuk"], "fa-laptop"),
            (["monitor", "monobloc", "monoblok", "display"], "fa-desktop"),
            (["kassa", "pos", "posterminal", "fiscal", "cash"], "fa-cash-register"),
            (["case", "desktop", "pc", "kompüter", "komputer"], "fa-computer"),
            (["ipad", "tab", "planşet"], "fa-tablet-screen-button"),
            (["domofon", "intercom"], "fa-door-closed"),
            (["access control", "card", "reader", "kart"], "fa-id-card"),
            (["telephone", "phone", "telefon", "ats", "yeastar"], "fa-phone"),
            (["access point", "wifi", "wireless", "unifi"], "fa-wifi"),
            (["switch", "hub", "router", "mikrotik", "gateway", "modem", "zte"], "fa-network-wired"),
            (["converter", "optika", "splitter", "multiplexer", "transmitter", "receiver", "ethernet"], "fa-ethernet"),
            (["server", "rack", "nas", "storage"], "fa-server"),
            (["hdd", "external box", "flash", "disk"], "fa-hard-drive"),
            (["dvd", "cd"], "fa-compact-disc"),
            (["dvr", "nvr", "camera", "kamera", "webcam", "obyektiv", "ştativ"], "fa-video"),
            (["ups", "stabilizator", "adapter", "power"], "fa-plug"),
            (["tv", "projector", "proyektor"], "fa-tv"),
            (["speaker", "audio", "səs"], "fa-volume-high"),
            (["light", "işıq"], "fa-lightbulb"),
            (["tərəzi", "scale"], "fa-scale-balanced"),
            (["tester", "multimetr", "diaqnostika"], "fa-gauge"),
            (["soldering", "screwdriver", "lehim"], "fa-screwdriver-wrench"),
            (["security", "təhlükəsizlik"], "fa-shield-halved"),
            (["fan"], "fa-fan")
        ];

        public static string For(string? categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName)) return Default;
            var name = categoryName.ToLowerInvariant();
            foreach (var (words, icon) in Rules)
                if (words.Any(word => Matches(name, word)))
                    return icon;
            return Default;
        }

        // Short words such as "pc", "tab" or "tv" match only as whole words, or "laptop" would read as "tab" and "stv" as "tv"
        private static bool Matches(string name, string word)
        {
            if (word.Length > 3) return name.Contains(word);
            var at = name.IndexOf(word, StringComparison.Ordinal);
            while (at >= 0)
            {
                var before = at == 0 || !char.IsLetter(name[at - 1]);
                var end = at + word.Length;
                var after = end == name.Length || !char.IsLetter(name[end]);
                if (before && after) return true;
                at = name.IndexOf(word, at + 1, StringComparison.Ordinal);
            }
            return false;
        }
    }
}
