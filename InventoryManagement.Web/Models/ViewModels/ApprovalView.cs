using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace InventoryManagement.Web.Models.ViewModels
{
    /// <summary>Reads a request's stored ActionData into a title and a list of field changes for the approval UI.</summary>
    public sealed class ApprovalView
    {
        /// <summary>One row of the diff, with Field as an English key the view translates.</summary>
        public sealed record Change(string Field, string? Current, string? Proposed, bool Localize = false);

        public string Kind { get; private init; } = "";
        public string Subject { get; private init; } = "";
        public List<Change> Changes { get; } = [];
        /// <summary>The part of ActionData that carries the image changes.</summary>
        public JObject? ImageData { get; private init; }
        /// <summary>For deletions the dialog shows a warning instead of a diff.</summary>
        public bool IsDeletion { get; private init; }
        /// <summary>A route update's new destination when the request stored only its id, for the dialog to name.</summary>
        public int? UnnamedDepartmentId { get; private set; }

        /// <summary>Shows the department name in place of the id in the To Department row.</summary>
        public void NameDepartment(string name)
        {
            var index = Changes.FindIndex(c => c.Field == "To Department");
            if (index >= 0 && !string.IsNullOrWhiteSpace(name))
                Changes[index] = Changes[index] with { Proposed = name };
            UnnamedDepartmentId = null;
        }

        private static readonly Regex FieldChange = new(@"^(Vendor|Model|Category|Department|Worker|Description|Color|Destination): (.*?) (?:→|->) (.*)$", RegexOptions.Singleline);

        // Change lines written as whole sentences instead of a field with old and new values
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
            Add(view, "Color", null, Str(d, "color"));
            Add(view, "Specifications", null, SpecificationText(d));
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
                else if (line.Trim() == "Specifications were updated")
                    view.Changes.Add(new("Specifications", null, update is null ? null : SpecificationText(update)));
                else if (Sentences.TryGetValue(line.Trim(), out var change))
                    view.Changes.Add(change);
                // The image change line is skipped here because the images block shows it
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
            var lines = RouteChangeLines(Get(d, "changes")).ToList();

            // The rows come from UpdateData, which is what approval applies, and the summary lines only fill in the old values
            if (update is not null && Get(update, "toDepartmentId") is JValue departmentId)
            {
                var line = lines.FirstOrDefault(l => l.StartsWith("Destination: ", StringComparison.Ordinal));
                var proposed = line is null ? null : ProposedAfter(line, "Destination: " + to + " -> ") ?? MatchGroup(line, 3);
                // Older requests named the new destination only by its id
                if (proposed is null || Regex.IsMatch(proposed, @"^department #\d+$"))
                {
                    proposed = "#" + departmentId;
                    if (int.TryParse(departmentId.ToString(), out var id))
                        view.UnnamedDepartmentId = id;
                }
                view.Changes.Add(new("To Department", to, proposed));
            }

            if (update is not null && Get(update, "toWorker") is JValue { Type: not JTokenType.Null } workerToken)
            {
                var proposed = workerToken.ToString();
                var line = lines.FirstOrDefault(l => l.StartsWith("Worker: ", StringComparison.Ordinal));
                // Cut the known new value off the end, because a name may itself contain an arrow
                var current = line is null ? null
                    : CurrentBefore(line, "Worker: ", " -> " + (string.IsNullOrWhiteSpace(proposed) ? "None" : proposed.Trim()))
                      ?? CurrentBefore(line, "Worker: ", " -> " + proposed)
                      ?? MatchGroup(line, 2);
                view.Changes.Add(new("Worker", current is null ? null : NoneToNull(current.Trim()), NoneToNull(proposed.Trim())));
            }

            // Requests stored without a change list show the notes whenever UpdateData carries them
            var notesChanged = lines.Count == 0
                ? update is not null && !string.IsNullOrWhiteSpace(Str(update, "notes"))
                : lines.Any(l => l is "Notes updated" or "Notes cleared");
            if (notesChanged)
            {
                var notes = update is null ? null : Str(update, "notes");
                view.Changes.Add(new("Notes", Str(d, "currentNotes"), notes == "{}" ? null : notes));
            }

            // Lines for fields UpdateData does not carry, kept so nothing the request lists is hidden
            foreach (var line in lines)
            {
                var m = FieldChange.Match(line);
                if (!m.Success) continue;
                var field = m.Groups[1].Value == "Destination" ? "To Department" : m.Groups[1].Value;
                if (view.Changes.All(c => c.Field != field))
                    view.Changes.Add(new(field, NoneToNull(m.Groups[2].Value), NoneToNull(m.Groups[3].Value)));
            }

            if (HasImageChanges(update))
                view.Changes.Add(new("Images", null, null));
            if (view.Changes.Count == 0 && from != null && to != null)
                view.Changes.Add(new("Route", null, $"{from} → {to}"));
            return view;
        }

        // Older requests joined the changes into one text with ", ", and a name may itself contain ", "
        private static readonly Regex RouteChangeSeparator =
            new(@"(?:, |; )(?=Notes updated|Notes cleared|Worker cleared|Worker: |Destination: |Images updated)");

        private static IEnumerable<string> RouteChangeLines(JToken? token) => token switch
        {
            JArray array => array.Select(t => t.ToString().Trim()),
            JValue { Type: JTokenType.String } value => RouteChangeSeparator.Split(value.ToString()).Select(l => l.Trim()),
            _ => []
        };

        private static string? MatchGroup(string line, int group)
            => FieldChange.Match(line) is { Success: true } m ? m.Groups[group].Value : null;

        private static string? ProposedAfter(string line, string prefix)
            => line.StartsWith(prefix, StringComparison.Ordinal) ? line[prefix.Length..] : null;

        private static string? CurrentBefore(string line, string prefix, string suffix)
            => line.Length >= prefix.Length + suffix.Length && line.StartsWith(prefix, StringComparison.Ordinal) && line.EndsWith(suffix, StringComparison.Ordinal)
                ? line[prefix.Length..^suffix.Length] : null;

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

        private static void Add(ApprovalView view, string field, string? current, string? proposed)
        {
            if (!string.IsNullOrWhiteSpace(current) || !string.IsNullOrWhiteSpace(proposed))
                view.Changes.Add(new(field, current, proposed));
        }

        /// <summary>Joins the specification lines into one name and value text, or null when there are none.</summary>
        private static string? SpecificationText(JObject d)
        {
            if (Get(d, "specifications") is not JArray lines || lines.Count == 0) return null;
            return string.Join("; ", lines.OfType<JObject>()
                .Select(l => $"{Str(l, "name")}: {Str(l, "value")}".TrimEnd(' ', ':')));
        }

        private static string SubjectOf(string? model, string? code)
            => string.Join(" · ", new[] { model, code }.Where(s => !string.IsNullOrWhiteSpace(s)));

        // Case-insensitive because older ActionData rows are PascalCase
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

        /// <summary>Counts of added and removed images for the Images row.</summary>
        /// <summary>The photos uploaded with the request in stored order, which the dialog's thumbnails address by index.</summary>
        public List<JObject> NewImages()
        {
            var list = new List<JObject>();
            if (ImageData is null) return list;
            foreach (var key in new[] { "images", "replaceImages" })
            {
                if (Get(ImageData, key) is JArray array)
                    list.AddRange(array.OfType<JObject>());
            }
            // Older requests stored a single image on the data itself
            if (Get(ImageData, "imageData") is JValue) list.Add(ImageData);
            return list;
        }

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
