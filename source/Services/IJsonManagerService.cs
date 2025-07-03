namespace AD_User_Reset_Print.Services
{
    public interface IJsonManagerService
    {
        /// <summary>
        /// Saves a list of items to a JSON file.
        /// </summary>
        /// <typeparam name="T">The type of items in the list.</typeparam>
        /// <param name="items">The list of items to save.</param>
        /// <param name="filePath">The full path to the JSON file.</param>
        /// <param name="overwrite">If true, the file will be completely overwritten with the new items. If false, the new items will be appended to the existing content.</param>
        void SaveToJson<T>(List<T> items, string filePath, bool overwrite = false);

        /// <summary>
        /// Reads a list of items from a JSON file.
        /// </summary>
        /// <typeparam name="T">The type of items to read.</typeparam>
        /// <param name="filePath">The full path to the JSON file.</param>
        /// <returns>A list of items read from the file, or an empty list if the file doesn't exist or is invalid.</returns>
        List<T> ReadFromJson<T>(string filePath);
    }
}