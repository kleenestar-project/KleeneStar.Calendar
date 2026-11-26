using KleeneStar.Core;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Calendar.WebAppointment
{
    /// <summary>
    /// Creates the object of a new appointment the way the core's object endpoint creates any
    /// object: one genesis commit holding the object row, the values its template presets and
    /// the entry state of every workflow of its class.
    /// </summary>
    public static class AppointmentObjects
    {
        /// <summary>
        /// The prefix of the presets the calendar reads from a template beside the field
        /// presets: <c>kleenestar.calendar:duration</c> and so on. The core's template wizard
        /// keeps such add-on keys as they are.
        /// </summary>
        public const string PresetPrefix = "kleenestar.calendar:";

        /// <summary>
        /// Creates an appointment object.
        /// </summary>
        /// <param name="class">The appointment class.</param>
        /// <param name="summary">The title.</param>
        /// <param name="description">The description (an editor document), or <see langword="null"/>.</param>
        /// <param name="creatorId">Who creates it, or <see cref="Guid.Empty"/>.</param>
        /// <param name="presets">The presets of the template it is created from, or <see langword="null"/>.</param>
        /// <returns>The object.</returns>
        public static ObjectEntity Create(Class @class, string summary, string description, Guid creatorId, IReadOnlyDictionary<string, string> presets = null)
        {
            ArgumentNullException.ThrowIfNull(@class);

            presets ??= new Dictionary<string, string>();

            if (string.IsNullOrWhiteSpace(summary) && presets.TryGetValue(nameof(ObjectEntity.Summary), out var presetSummary))
            {
                summary = presetSummary;
            }

            if (string.IsNullOrWhiteSpace(description) && presets.TryGetValue(nameof(ObjectEntity.Description), out var presetDescription))
            {
                description = presetDescription;
            }

            var actor = creatorId == Guid.Empty ? (Guid?)null : creatorId;
            var entity = new ObjectEntity(Guid.NewGuid())
            {
                Key = CoreHub.ObjectManager.NextObjectKey(@class.WorkspaceId),
                Summary = string.IsNullOrWhiteSpace(summary) ? "?" : summary.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description,
                Icon = @class.Icon,
                State = WorkspaceState.Active,
                Kind = ObjectKind.Appointment,
                WorkspaceId = @class.WorkspaceId,
                ClassId = @class.Id,
                SecurityLevelId = CoreHub.SecurityLevelManager?.GetDefaultSecurityLevel(@class.Id)?.Id,
                CreatorId = actor,
                UpdaterId = actor,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            };

            using (CoreHub.CommitManager.BeginCommit(entity.Id, CommitType.Created, creatorId))
            {
                CoreHub.ObjectManager.Add(entity);

                ApplyFieldPresets(entity, presets);
                StartWorkflows(entity);
            }

            return entity;
        }

        /// <summary>
        /// Writes the presets of a template that name a field of the object's class. Workflow,
        /// attachment and password fields take no preset, as in the core's template wizard.
        /// </summary>
        /// <param name="entity">The object.</param>
        /// <param name="presets">The presets.</param>
        private static void ApplyFieldPresets(ObjectEntity entity, IReadOnlyDictionary<string, string> presets)
        {
            if (presets.Count == 0)
            {
                return;
            }

            var fields = CoreHub.FieldManager
                .GetFields(new ClassIdParameter(entity.ClassId))
                .Where(f => !f.Deprecated && f.State == FieldState.Active)
                .Where(f => f.FieldType is not (FieldType.Workflow or FieldType.Attachment or FieldType.Password))
                .Where(f => !string.Equals(f.Name, nameof(ObjectEntity.Summary), StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(f.Name, nameof(ObjectEntity.Description), StringComparison.OrdinalIgnoreCase));

            foreach (var field in fields)
            {
                var preset = presets.FirstOrDefault(x => string.Equals(x.Key, field.Name, StringComparison.OrdinalIgnoreCase));

                if (string.IsNullOrWhiteSpace(preset.Value))
                {
                    continue;
                }

                CoreHub.ValueManager.Add(new Value
                {
                    ObjectId = entity.Id,
                    FieldId = field.Id,
                    Data = preset.Value,
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Places every workflow field of the object that holds no state on the first entry state
        /// of its workflow - what the core's object endpoint does for every new object.
        /// </summary>
        /// <param name="entity">The object.</param>
        public static void StartWorkflows(ObjectEntity entity)
        {
            var fields = CoreHub.FieldManager
                .GetFields(new ClassIdParameter(entity.ClassId))
                .Where(f => !f.Deprecated && f.State == FieldState.Active && f.FieldType == FieldType.Workflow && f.WorkflowId.HasValue);

            foreach (var field in fields)
            {
                var workflow = CoreHub.WorkflowManager.GetWorkflowWithStructure(field.WorkflowId.Value);
                var entry = workflow is null ? null : CoreHub.WorkflowManager.GetTargetStatuses(workflow, null).FirstOrDefault();

                if (entry is null)
                {
                    continue;
                }

                var existing = CoreHub.ValueManager.GetValue(entity.Id, field.Id);

                if (existing is null)
                {
                    CoreHub.ValueManager.Add(new Value
                    {
                        ObjectId = entity.Id,
                        FieldId = field.Id,
                        Data = entry.Name,
                        Created = DateTime.UtcNow,
                        Updated = DateTime.UtcNow
                    });
                }
                else if (string.IsNullOrWhiteSpace(existing.Data))
                {
                    existing.Data = entry.Name;
                    existing.Updated = DateTime.UtcNow;
                    CoreHub.ValueManager.Update(existing);
                }
            }
        }

        /// <summary>
        /// Reads the calendar presets of a template (those with <see cref="PresetPrefix"/>), keyed
        /// by the name after the prefix.
        /// </summary>
        /// <param name="presets">The presets of the template.</param>
        /// <returns>The calendar presets.</returns>
        public static IReadOnlyDictionary<string, string> CalendarPresets(IReadOnlyDictionary<string, string> presets)
        {
            return (presets ?? new Dictionary<string, string>())
                .Where(x => x.Key.StartsWith(PresetPrefix, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(x => x.Key[PresetPrefix.Length..], x => x.Value, StringComparer.OrdinalIgnoreCase);
        }
    }
}
