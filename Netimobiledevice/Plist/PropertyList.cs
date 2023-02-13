using System.IO;
using System.Text;
using System.Xml;

namespace Netimobiledevice.Plist
{
    /// <summary>
    /// Parses, saves, and creates a Plist file
    /// </summary>
    internal static class PropertyList
    {
        private static bool IsFormatBinary(Stream stream)
        {
            byte[] buf = new byte[8];
            // Read in first 8 bytes
            stream.Read(buf, 0, buf.Length);
            // Rewind
            stream.Seek(0, SeekOrigin.Begin);
            // Compare to known indicator
            // TODO: validate version as well
            return Encoding.UTF8.GetString(buf, 0, 6) == "bplist";
        }

        private static PropertyNode LoadAsBinary(Stream stream)
        {
            var reader = new BinaryFormatReader();
            return reader.Read(stream);
        }

        private static PropertyNode LoadAsXml(Stream stream)
        {
            // Set resolver to null in order to avoid calls to apple.com to resolve DTD
            var settings = new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Ignore,
            };

            using (var reader = XmlReader.Create(stream, settings)) {
                reader.MoveToContent();
                reader.ReadStartElement("plist");

                reader.MoveToContent();
                PropertyNode node = NodeFactory.Create(reader.LocalName);
                node.ReadXml(reader);

                reader.ReadEndElement();

                return node;
            }
        }

        /// <summary>
        /// Saves the Plist to the specified stream.
        /// </summary>
        /// <param name="rootNode">Root node of the Plist structure.</param>
        /// <param name="stream">The stream in which the PList is saves.</param>
        /// <param name="format">The format of the Plist (Binary/Xml).</param>
        private static void Save(PropertyNode rootNode, Stream stream, PlistFormat format)
        {
            if (format == PlistFormat.Xml) {
                const string newLine = "\n";

                var sets = new XmlWriterSettings {
                    Encoding = Encoding.UTF8,
                    Indent = true,
                    IndentChars = "\t",
                    NewLineChars = newLine,
                };

                using (var tmpStream = new MemoryStream()) {
                    using (var xmlWriter = XmlWriter.Create(tmpStream, sets)) {
                        xmlWriter.WriteStartDocument();
                        xmlWriter.WriteDocType("plist", "-//Apple Computer//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null);

                        // write out nodes, wrapped in plist root element
                        xmlWriter.WriteStartElement("plist");
                        xmlWriter.WriteAttributeString("version", "1.0");
                        rootNode.WriteXml(xmlWriter);
                        xmlWriter.WriteEndElement();
                        xmlWriter.Flush();
                    }

                    // XmlWriter always inserts a space before element closing (e.g. <true />)
                    // whereas the Apple parser can't deal with the space and expects <true/>
                    tmpStream.Seek(0, SeekOrigin.Begin);
                    using (var reader = new StreamReader(tmpStream)) {
                        using (var writer = new StreamWriter(stream, Encoding.UTF8, 4096, true)) {
                            writer.NewLine = newLine;
                            for (string? line = reader.ReadLine(); line != null; line = reader.ReadLine()) {
                                if (line.Trim() == "<true />") {
                                    line = line.Replace("<true />", "<true/>");
                                }
                                if (line.Trim() == "<false />") {
                                    line = line.Replace("<false />", "<false/>");
                                }
                                writer.WriteLine(line);
                            }
                        }
                    }
                }
            }
            else {
                var writer = new BinaryFormatWriter();
                writer.Write(stream, rootNode);
            }
        }

        /// <summary>
        /// Loads the Plist from specified stream.
        /// </summary>
        /// <param name="stream">The stream containing the Plist.</param>
        /// <returns>A <see cref="PropertyNode"/> object loaded from the stream</returns>
        public static PropertyNode Load(Stream stream)
        {
            bool isBinary = IsFormatBinary(stream);
            // Detect binary format, and read using the appropriate method
            return isBinary ? LoadAsBinary(stream) : LoadAsXml(stream);
        }

        /// <summary>
        /// Exports the Plist as a byte array.
        /// </summary>
        /// <param name="rootNode">Root node of the Plist structure.</param>
        /// <param name="format">The format of the Plist (Binary/Xml).</param>
        /// <returns>The byte array representation of the plist</returns>
        public static byte[] SaveAsByteArray(PropertyNode rootNode, PlistFormat format)
        {
            using (MemoryStream ms = new MemoryStream()) {
                Save(rootNode, ms, format);
                ms.Seek(0, SeekOrigin.Begin);
                return ms.ToArray();
            }
        }
    }
}
