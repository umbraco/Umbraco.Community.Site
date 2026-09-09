using Umbraco.Forms.Core.Data.Storage;
using Umbraco.Forms.Core.Models;
using Umbraco.Forms.Core.Persistence.Dtos;

namespace UmbracoCommunity.MeetBooking.Forms;

/// <summary>Saves a record whose field values were changed in memory. Behind an interface so the workflow is testable without Forms storage.</summary>
public interface IRecordPersister
{
    /// <param name="newFields">Record fields the mapper created (not yet in the database) — inserted rather than updated.</param>
    void Save(Record record, Form form, IReadOnlyCollection<RecordField> newFields);
}

/// <summary>
/// Persists via Forms' own storage. <see cref="IRecordStorage.UpdateRecord(Record, Form)"/> writes the record and its
/// existing fields; a <see cref="RecordField"/> the mapper had to create (hidden field added to the form after the
/// entry was submitted) is not in the database yet and is inserted explicitly first.
/// </summary>
public sealed class FormsRecordPersister(IRecordStorage records, IRecordFieldStorage recordFields) : IRecordPersister
{
    public void Save(Record record, Form form, IReadOnlyCollection<RecordField> newFields)
    {
        foreach (var field in newFields)
        {
            field.Record = record.Id;
            recordFields.InsertRecordField(field);
        }

        records.UpdateRecord(record, form);
    }
}
