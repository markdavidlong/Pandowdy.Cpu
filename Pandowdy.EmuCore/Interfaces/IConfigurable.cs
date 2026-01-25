namespace Pandowdy.EmuCore.Interfaces;

/// <summary>
/// Represents an object that can persist and restore its configuration state via metadata strings.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="IConfigurable"/> interface provides a simple, format-agnostic mechanism for
/// objects to serialize and deserialize their configuration state. Implementers choose their
/// own metadata format (JSON, YAML, XML, binary, etc.) based on their complexity and requirements.
/// </para>
/// <para>
/// <strong>Design Philosophy:</strong>
/// </para>
/// <list type="bullet">
/// <item><description><strong>Format Freedom:</strong> Each implementation chooses its own serialization format</description></item>
/// <item><description><strong>Self-Contained:</strong> Metadata strings are opaque to consumers</description></item>
/// <item><description><strong>Fail-Safe:</strong> <see cref="ApplyMetadata"/> returns success status rather than throwing exceptions</description></item>
/// <item><description><strong>Stateless Protocol:</strong> No assumptions about when/how metadata is captured or applied</description></item>
/// <item><description><strong>Hierarchical Freedom:</strong> Objects may include child configurations or manage them separately</description></item>
/// </list>
/// <para>
/// <strong>Common Use Cases:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Persisting peripheral card configurations (drives, settings, etc.)</description></item>
/// <item><description>Saving/loading emulator state snapshots</description></item>
/// <item><description>Cloning configured objects with preserved state</description></item>
/// <item><description>Exporting/importing configurations between sessions</description></item>
/// </list>
/// <para>
/// <strong>Hierarchical Configuration:</strong><br/>
/// Objects with child components (e.g., a disk controller with attached drives) have flexibility
/// in how they handle child configuration:
/// </para>
/// <list type="bullet">
/// <item><description><strong>Inclusive Approach:</strong> Parent includes child metadata within its own (recommended for tight coupling)</description></item>
/// <item><description><strong>Separate Approach:</strong> Parent and children managed independently (useful for loose coupling)</description></item>
/// <item><description><strong>Hybrid Approach:</strong> Some children included, others referenced externally</description></item>
/// </list>
/// <para>
/// <strong>Example: Disk Controller with Drives</strong>
/// </para>
/// <code>
/// // Inclusive approach - controller includes drive configurations
/// {
///   "controllerSettings": { "motorSpeed": "normal" },
///   "drives": [
///     { "slot": 0, "imagePath": "System.dsk", "writeProtected": true },
///     { "slot": 1, "imagePath": "Data.dsk", "writeProtected": false }
///   ]
/// }
/// 
/// // When applying metadata, the controller:
/// // 1. Reads its own settings
/// // 2. Creates/configures child drives based on embedded configuration
/// // 3. Recursively applies metadata to each child
/// </code>
/// <para>
/// This approach keeps the configuration self-contained and ensures all related state
/// travels together. The parent is responsible for creating and initializing its children
/// during <see cref="ApplyMetadata"/>.
/// </para>
/// <para>
/// <strong>Implementation Guidelines:</strong>
/// </para>
/// <list type="number">
/// <item><description><see cref="GetMetadata"/> should never throw exceptions (return empty string on error)</description></item>
/// <item><description><see cref="ApplyMetadata"/> should validate input and return <c>false</c> on failure</description></item>
/// <item><description>Metadata format should be documented in implementing class remarks</description></item>
/// <item><description>Consider including version information in metadata for backward compatibility</description></item>
/// <item><description>Empty string metadata should represent default/unconfigured state</description></item>
/// <item><description>For hierarchical objects, decide whether children are included or referenced</description></item>
/// <item><description>Document the hierarchical strategy in the implementing class</description></item>
/// </list>
/// </remarks>
/// <example>
/// <para>
/// <strong>Simple Implementation (JSON-based):</strong>
/// </para>
/// <code>
/// public class SimpleDevice : IConfigurable
/// {
///     public string DeviceName { get; set; } = "Unnamed";
///     public bool IsEnabled { get; set; } = true;
///     
///     public string GetMetadata()
///     {
///         try
///         {
///             return JsonSerializer.Serialize(new 
///             { 
///                 DeviceName, 
///                 IsEnabled 
///             });
///         }
///         catch
///         {
///             return string.Empty; // Fail-safe
///         }
///     }
///     
///     public bool ApplyMetadata(string metadata)
///     {
///         if (string.IsNullOrWhiteSpace(metadata))
///         {
///             // Reset to defaults
///             DeviceName = "Unnamed";
///             IsEnabled = true;
///             return true;
///         }
///         
///         try
///         {
///             var config = JsonSerializer.Deserialize&lt;dynamic&gt;(metadata);
///             DeviceName = config.DeviceName;
///             IsEnabled = config.IsEnabled;
///             return true;
///         }
///         catch
///         {
///             return false; // Invalid metadata
///         }
///     }
/// }
/// 
/// // Usage
/// var device = new SimpleDevice { DeviceName = "Serial Port", IsEnabled = false };
/// string metadata = device.GetMetadata();
/// 
/// var newDevice = new SimpleDevice();
/// if (newDevice.ApplyMetadata(metadata))
/// {
///     Console.WriteLine($"Restored: {newDevice.DeviceName}");
/// }
/// </code>
/// <para>
/// <strong>Hierarchical Implementation (Controller with Drives):</strong>
/// </para>
/// <code>
/// public class DiskController : IConfigurable
/// {
///     private List&lt;DiskDrive&gt; _drives = new();
///     
///     public string GetMetadata()
///     {
///         try
///         {
///             var config = new
///             {
///                 Version = 1,
///                 ControllerSettings = new { /* controller-specific settings */ },
///                 Drives = _drives.Select(d => new
///                 {
///                     Slot = d.Slot,
///                     // Include child's metadata inline
///                     DriveConfig = d.GetMetadata()
///                 }).ToArray()
///             };
///             
///             return JsonSerializer.Serialize(config);
///         }
///         catch
///         {
///             return string.Empty;
///         }
///     }
///     
///     public bool ApplyMetadata(string metadata)
///     {
///         if (string.IsNullOrWhiteSpace(metadata))
///         {
///             _drives.Clear();
///             return true;
///         }
///         
///         try
///         {
///             var config = JsonSerializer.Deserialize&lt;dynamic&gt;(metadata);
///             
///             // Apply controller settings first
///             // ... apply controller-specific configuration ...
///             
///             // Recreate drives based on embedded configuration
///             _drives.Clear();
///             foreach (var driveConfig in config.Drives)
///             {
///                 var drive = new DiskDrive(driveConfig.Slot);
///                 
///                 // Recursively apply child metadata
///                 if (!drive.ApplyMetadata(driveConfig.DriveConfig.ToString()))
///                 {
///                     return false; // Child configuration failed
///                 }
///                 
///                 _drives.Add(drive);
///             }
///             
///             return true;
///         }
///         catch
///         {
///             return false;
///         }
///     }
/// }
/// </code>
/// </example>
/// <seealso cref="ICard"/>
public interface IConfigurable
{
    /// <summary>
    /// Gets the current configuration state as a metadata string.
    /// </summary>
    /// <returns>
    /// A string containing serialized configuration data in an implementation-specific format,
    /// or an empty string if the object has no configuration state or if serialization fails.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method captures the current configuration state of the object in a format suitable
    /// for persistence, transmission, or cloning. The format is entirely up to the implementer
    /// and should be documented in the implementing class.
    /// </para>
    /// <para>
    /// <strong>Format Examples:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description><strong>Simple objects:</strong> JSON or key-value pairs</description></item>
    /// <item><description><strong>Complex objects:</strong> YAML, XML, or custom formats</description></item>
    /// <item><description><strong>Binary data:</strong> Base64-encoded binary blob</description></item>
    /// <item><description><strong>No state:</strong> Empty string</description></item>
    /// <item><description><strong>Hierarchical objects:</strong> Nested structures with child configurations embedded</description></item>
    /// </list>
    /// <para>
    /// <strong>Hierarchical Objects:</strong><br/>
    /// Objects with child components that also implement <see cref="IConfigurable"/> may choose to:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Embed child metadata within the parent's metadata (keeps configuration self-contained)</description></item>
    /// <item><description>Reference children externally (allows independent child management)</description></item>
    /// <item><description>Use a hybrid approach based on business logic requirements</description></item>
    /// </list>
    /// <para>
    /// The choice depends on the relationship between parent and childâ€”tightly coupled components
    /// (like a controller and its drives) typically use embedded metadata, while loosely coupled
    /// components might prefer external references.
    /// </para>
    /// <para>
    /// <strong>Error Handling:</strong><br/>
    /// This method should never throw exceptions. If serialization fails for any reason,
    /// return an empty string. The caller can choose to treat empty string as "no configuration"
    /// or as an error condition.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong><br/>
    /// Implementations should ensure this method is safe to call from any thread, though
    /// concurrent modification during serialization may produce inconsistent results.
    /// </para>
    /// </remarks>
    string GetMetadata();

    /// <summary>
    /// Applies configuration metadata to this object, restoring its state.
    /// </summary>
    /// <param name="metadata">
    /// A metadata string previously obtained from <see cref="GetMetadata"/>, or an empty
    /// string to reset to default configuration.
    /// </param>
    /// <returns>
    /// <c>true</c> if the metadata was successfully parsed and applied; <c>false</c> if
    /// the metadata was invalid, incompatible, or could not be applied for any reason.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method attempts to restore the object's configuration state from a metadata string.
    /// The method should validate the metadata format and contents before applying any changes.
    /// If validation fails at any point, the method should return <c>false</c> and leave the
    /// object in a consistent state (preferably unchanged).
    /// </para>
    /// <para>
    /// <strong>Empty String Handling:</strong><br/>
    /// Passing an empty or whitespace-only string should reset the object to its default
    /// configuration state and return <c>true</c>. This provides a standard way to clear
    /// configuration without needing format-specific knowledge.
    /// </para>
    /// <para>
    /// <strong>Hierarchical Configuration:</strong><br/>
    /// For objects with child components, <see cref="ApplyMetadata"/> is responsible for:
    /// </para>
    /// <list type="number">
    /// <item><description>Parsing the parent's configuration from metadata</description></item>
    /// <item><description>Creating or locating child objects based on metadata</description></item>
    /// <item><description>Recursively applying embedded child metadata to each child</description></item>
    /// <item><description>Validating the entire configuration hierarchy</description></item>
    /// </list>
    /// <para>
    /// If any child fails to apply its metadata, the parent should return <c>false</c> and
    /// either roll back all changes or leave the object in a consistent partial state
    /// (documented by the implementing class).
    /// </para>
    /// <para>
    /// <strong>Common Failure Scenarios:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description><strong>Format errors:</strong> Malformed JSON/XML, invalid syntax</description></item>
    /// <item><description><strong>Schema mismatch:</strong> Missing required fields, unknown fields</description></item>
    /// <item><description><strong>Version incompatibility:</strong> Metadata from newer version</description></item>
    /// <item><description><strong>Resource errors:</strong> Referenced files don't exist, resources unavailable</description></item>
    /// <item><description><strong>Validation errors:</strong> Values out of range, invalid combinations</description></item>
    /// <item><description><strong>Child configuration failure:</strong> Child object failed to apply its embedded metadata</description></item>
    /// </list>
    /// <para>
    /// <strong>Partial Application:</strong><br/>
    /// Implementations should apply changes atomically when possible. If metadata contains
    /// multiple settings and one fails validation, the implementation should either apply
    /// none of them (transactional) or document which settings were applied (partial).
    /// The return value indicates overall success/failure.
    /// </para>
    /// <para>
    /// <strong>Error Handling:</strong><br/>
    /// This method should never throw exceptions. All errors should result in returning
    /// <c>false</c>. Implementations may log errors for diagnostic purposes.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Successful application
    /// var device = new Device();
    /// if (device.ApplyMetadata("{\"name\":\"COM1\",\"baud\":9600}"))
    /// {
    ///     Console.WriteLine("Configuration applied successfully");
    /// }
    /// 
    /// // Reset to defaults
    /// if (device.ApplyMetadata(string.Empty))
    /// {
    ///     Console.WriteLine("Reset to default configuration");
    /// }
    /// 
    /// // Handle failure
    /// if (!device.ApplyMetadata("invalid metadata"))
    /// {
    ///     Console.WriteLine("Failed to apply configuration");
    /// }
    /// 
    /// // Hierarchical application
    /// var controller = new DiskController();
    /// string metadata = @"{
    ///     ""drives"": [
    ///         { ""slot"": 0, ""driveConfig"": ""{...}"" },
    ///         { ""slot"": 1, ""driveConfig"": ""{...}"" }
    ///     ]
    /// }";
    /// 
    /// if (controller.ApplyMetadata(metadata))
    /// {
    ///     // Controller and all drives configured successfully
    /// }
    /// </code>
    /// </example>
    bool ApplyMetadata(string metadata);
}
