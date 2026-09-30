using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace InventoryManagement.Web.Models.ViewModels
{
    /// <summary>
    /// What an approval request is about, read from its stored ActionData (camelCase or the old
    /// PascalCase shapes): a kind and subject for the list ("Product update: Latitude 5420 · 1042"),
    /// and the fields it changes as current -> proposed pairs for the decision dialog.
    /// Field names and <see cref="Change.Localize"/> values are English keys, translated by the view.
    /// </summary>
    public sealed class ApprovalView
    {
        public sealed record Change(string Field, string? Current, string? Proposed, bool Localize = false);

        public string Kind { get; private init; } = "";
        public string Subject { get; private init; } = "";
        public List<Change> Changes { get; } = [];
        /// <summary>The object that carries image changes (images, replaceImages, removeImageUrls, coverImageUrl).</summary>
        public JObject? ImageData { get; private init; }
        /// <summary>Set for deletions: the dialog warns instead of showing a diff.</summary>
        public bool IsDeletion { get; private init; }

        private static readonly Regex FieldChange = new(@"^(Vendor|Model|Category|Department|Worker|Description|Destination): (.*?) (?:→|->) (.*)$", RegexOptions.Singleline);

        private static readonly Dictionary<string, Change> Sentences = new()
        {
            ["Product is working now"] = new("Working state", "Not working", "Working", true),
            ["Product is not working"] = new("Working state", "Working", "Not working", true),
            ["Product is active now"] = new("Availability", "Inactive", "Active", true),
            ["Product is not available"] = new("Availability", "Active", "Inactive", true),
            ["Product is new now"] = new("Condition", "Used", "New", true),
            ["Product's status changed to old"] = new("Condition", "New", "Used", true),
        };

        public static ApprovalView From(string requestType, string? actionData)
        {
            JObject data;
            try { data = JObject.Parse(string.IsNullOrWhiteSpace(actionData) ? "{}" : actionData); }
            catch { data = new JObject(); }

            return requestType switch
            {
                "product.create" => ProductCreate(Obj(data, "productData") ?? data),
                "product.update" => ProductUpdate(data),
                "product.delete" => ProductDelete(data),
                "product.transfer" => Transfer(data),
                "route.update" => RouteUpdate(data),
                "route.delete" => RouteDelete(data),
                _ => new ApprovalView { Kind = requestType }
            };
        }

        /// <summary>The changed field names, for the list row.</summary>
        public IEnumerable<string> FieldNames => Changes.Select(c => c.Field).Distinct();

        private static ApprovalView ProductCreate(JObject d)
        {
            var view = new ApprovalView
            {
                Kind = "New product",
                Subject = SubjectOf(Str(d, "model"), Str(d, "inventoryCode")),
                ImageData = d
            };
            Add(view, "Inventory Code", null, Str(d, "inventoryCode"));
            Add(view, "Model", null, Str(d, "model"));
            Add(view, "Vendor", null, Str(d, "vendor"));
            Add(view, "Category", null, Str(d, "categoryName"));
            Add(view, "Department", null, Str(d, "departmentName"));
            Add(view, "Worker", null, Str(d, "worker"));
            Add(view, "Description", null, Str(d, "description"));
            view.Changes.Add(new("Working state", null, Bool(d, "isWorking", true) ? "Working" : "Not working", true));
            view.Changes.Add(new("Availability", null, Bool(d, "isActive", true) ? "Active" : "Inactive", true));
            if (Bool(d, "isNewItem", false))
                view.Changes.Add(new("Condition", null, "New", true));
            return view;
        }

        private static ApprovalView ProductUpdate(JObject d)
        {
            var update = Obj(d, "updateData");
            var view = new ApprovalView
            {
                Kind = "Product update",
                Subject = SubjectOf(update is null ? null : Str(update, "model"), Str(d, "inventoryCode")),
                ImageData = update
            };
            foreach (var line in Lines(d["changes"] ?? d["Changes"]))
            {
                var m = FieldChange.Match(line);
                if (m.Success)
                    view.Changes.Add(new(m.Groups[1].Value, NoneToNull(m.Groups[2].Value), NoneToNull(m.Groups[3].Value)));
                else if (Sentences.TryGetValue(line.Trim(), out var change))
                    view.Changes.Add(change);
                // "Product images were updated": shown by the images block.
            }
            if (HasImageChanges(update) && view.Changes.All(c => c.Field != "Images"))
                view.Changes.Add(new("Images", null, null));
            return view;
        }

        private static ApprovalView ProductDelete(JObject d)
        {
            var view = new ApprovalView
            {
                Kind = "Product deletion",
                Subject = SubjectOf(Str(d, "model"), Str(d, "inventoryCode")),
                IsDeletion = true
            };
            Add(view, "Inventory Code", Str(d, "inventoryCode"), null);
            Add(view, "Model", Str(d, "model"), null);
            Add(view, "Vendor", Str(d, "vendor"), null);
            Add(view, "Department", Str(d, "departmentName"), null);
            return view;
        }

        private static ApprovalView Transfer(JObject d)
        {
            var view = new ApprovalView
            {
                Kind = "Transfer",
                Subject = SubjectOf(Str(d, "productModel"), Str(d, "inventoryCode")),
                ImageData = d
            };
            view.Changes.Add(new("Department", Str(d, "fromDepartmentName"), Str(d, "toDepartmentName")));
            view.Changes.Add(new("Worker", Str(d, "fromWorker"), Str(d, "toWorker")));
            var notes = Str(d, "notes");
            if (!string.IsNullOrWhiteSpace(notes) && notes != "{}")
                view.Changes.Add(new("Notes", null, notes));
            if (HasImageChanges(d))
                view.Changes.Add(new("Images", null, null));
            return view;
        }

        private static ApprovalView RouteUpdate(JObject d)
        {
            var update = Obj(d, "updateData");
            var view = new ApprovalView
            {
                Kind = "Route update",
                Subject = SubjectOf(Str(d, "model"), Str(d, "inventoryCode")),
                ImageData = update
            };
            var from = Str(d, "fromDepartmentName");
            var to = Str(d, "toDepartmentName");
            foreach (var line in Lines(d["changes"] ?? d["Changes"]).SelectMany(l => l.Split("; ")))
            {
                var m = FieldChange.Match(line.Trim());
                if (m.Success)
                    view.Changes.Add(new(m.Groups[1].Value == "Destination" ? "To Department" : m.Groups[1].Value,
                        NoneToNull(m.Groups[2].Value), NoneToNull(m.Groups[3].Value)));
            }
            var notes = update is null ? null : Str(update, "notes");
            if (!string.IsNullOrWhiteSpace(notes) && notes != "{}")
                view.Changes.Add(new("Notes", null, notes));
            if (HasImageChanges(update))
                view.Changes.Add(new("Images", null, null));
            if (view.Changes.Count == 0 && from != null && to != null)
                view.Changes.Add(new("Route", null, $"{from} → {to}"));
            return view;
        }

        private static ApprovalView RouteDelete(JObject d)
        {
            var routeId = Str(d, "routeId");
            var view = new ApprovalView
            {
                Kind = "Route deletion",
                Subject = Str(d, "productInfo") ?? (routeId is null ? "" : "#" + routeId),
                IsDeletion = true
            };
            Add(view, "Product", Str(d, "productInfo"), null);
            Add(view, "From", Str(d, "fromLocation"), null);
            Add(view, "To", Str(d, "toLocation"), null);
            return view;
        }

        // ---- helpers --------------------------------------------------------------------

        private static void Add(ApprovalView view, string field, string? current, string? proposed)
        {
            if (!string.IsNullOrWhiteSpace(current) || !string.IsNullOrWhiteSpace(proposed))
                view.Changes.Add(new(field, current, proposed));
        }

        private static string SubjectOf(string? model, string? code)
            => string.Join(" · ", new[] { model, code }.Where(s => !string.IsNullOrWhiteSpace(s)));

        private static JToken? Get(JObject o, string name) => o.GetValue(name, StringComparison.OrdinalIgnoreCase);

        private static JObject? Obj(JObject o, string name) => Get(o, name) as JObject;

        private static string? Str(JObject o, string name)
        {
            var token = Get(o, name);
            return token is null || token.Type is JTokenType.Null or JTokenType.Object or JTokenType.Array ? null : token.ToString();
        }

        private static bool Bool(JObject o, string name, bool fallback)
            => Get(o, name) is JValue { Type: JTokenType.Boolean } v ? v.Value<bool>() : fallback;

        private static IEnumerable<string> Lines(JToken? token) => token switch
        {
            JArray array => array.Select(t => t.ToString()),
            JValue { Type: JTokenType.String } value => [value.ToString()],
            _ => []
        };

        private static string? NoneToNull(string value)
            => string.IsNullOrWhiteSpace(value) || value == "None" ? null : value;

        private static bool HasImageChanges(JObject? d)
            => d is not null && (Get(d, "images") is JArray { Count: > 0 } || Get(d, "replaceImages") is JArray { Count: > 0 }
                                 || Get(d, "removeImageUrls") is JArray { Count: > 0 } || Get(d, "coverImageUrl") is JValue
                                 || Get(d, "imageData") is JValue);

        /// <summary>New files and removed current images in <see cref="ImageData"/> (the "Images" row).</summary>
        public (int Added, int Removed) ImageCounts()
        {
            if (ImageData is null) return (0, 0);
            var added = (Get(ImageData, "images") as JArray)?.Count ?? 0;
            added += (Get(ImageData, "replaceImages") as JArray)?.Count ?? 0;
            if (Get(ImageData, "imageData") is JValue) added++;
            var removed = (Get(ImageData, "removeImageUrls") as JArray)?.Count ?? 0;
            return (added, removed);
        }
    }
}
