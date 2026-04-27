namespace AhuErp.Core.Services
{
    /// <summary>
    /// Результат полнотекстового поиска (<see cref="ISearchIndexService.FullTextSearch"/>).
    /// Один hit = один (документ, вложение)-pair с TF-score и snippet'ом ±80 символов.
    /// </summary>
    public sealed class SearchHit
    {
        public int DocumentId { get; set; }
        public string DocumentTitle { get; set; }
        public string RegistrationNumber { get; set; }
        public int AttachmentId { get; set; }
        public string AttachmentName { get; set; }
        public string Snippet { get; set; }
        public double Score { get; set; }
    }
}
