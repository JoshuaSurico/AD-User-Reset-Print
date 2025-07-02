namespace AD_User_Reset_Print.Services
{
    public interface IJsonManagerService
    {
        /// <summary>
        /// Saves a list of items to a JSON file, optionally clearing existing content.
        /// </summary>
        /// <typeparam name="T">The type of items in the list.</typeparam>
        /// <param name="items">The list of items to save.</param>
        /// <param name="filePath">The full path to the JSON file.</param>
        /// <param name="clearExisting">If true, clears the file before saving. Otherwise, appends to existing data.</param>
        void SaveToJson<T>(List<T> items, string filePath, bool clearExisting = false);

        /// <summary>
        /// Reads a list of items from a JSON file.
        /// </summary>
        /// <typeparam name="T">The type of items to read.</typeparam>
        /// <param name="filePath">The full path to the JSON file.</param>
        /// <returns>A list of items read from the file, or an empty list if the file doesn't exist or is invalid.</returns>
        List<T> ReadFromJson<T>(string filePath);
    }
}