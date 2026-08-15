using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Collections.Concurrent;
using System.IO.Abstractions;
using Database.Domain.Entities;
using Database.Infrastructure.Data;
using Domain.Enums;
using Graph.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Graph.Infrastructure.Services;

public class XmpService(
    IFileSystem fileSystem,
    IServiceScopeFactory scopeFactory,
    IPathService pathService) : IXmpService {

    public static ConcurrentDictionary<string, DateTime> RecentWrites { get; } = new(StringComparer.OrdinalIgnoreCase);

    private static readonly XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private static readonly XNamespace xmp = "http://ns.adobe.com/xap/1.0/";
    private static readonly XNamespace dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace photoshop = "http://ns.adobe.com/photoshop/1.0/";
    private static readonly XNamespace lr = "http://ns.adobe.com/lightroom/1.0/";

    public async Task LoadMetadataAsync(Picture picture) {
        if (picture.SubFolder == null) {
            pathService.PopulatePaths(picture);
        }

        if (picture.SubFolder == null || string.IsNullOrEmpty(picture.SubFolder.Raw)) {
            picture.Rating = 0;
            picture.ColorLabel = ColorLabel.None;
            picture.CurationStatus = CurationStatus.Unflagged;
            return;
        }

        var xmpPath = fileSystem.Path.ChangeExtension(picture.SubFolder.Raw, ".xmp");

        if (!fileSystem.File.Exists(xmpPath)) {
            picture.Rating = 0;
            picture.ColorLabel = ColorLabel.None;
            picture.CurationStatus = CurationStatus.Unflagged;
            return;
        }

        try {
            // Read file with sharing permissions since other editors might lock/open it
            string content = "";
            const int maxRetries = 5;
            for (int i = 0; i < maxRetries; i++) {
                try {
                    using (var stream = fileSystem.FileStream.New(xmpPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var reader = new StreamReader(stream)) {
                        content = await reader.ReadToEndAsync();
                    }
                    break;
                } catch (IOException) when (i < maxRetries - 1) {
                    await Task.Delay(100);
                }
            }

            if (string.IsNullOrWhiteSpace(content)) {
                picture.Rating = 0;
                picture.ColorLabel = ColorLabel.None;
                picture.CurationStatus = CurationStatus.Unflagged;
                return;
            }

            var doc = XDocument.Parse(content);
            var desc = doc.Descendants(rdf + "Description").FirstOrDefault();
            if (desc == null) {
                picture.Rating = 0;
                picture.ColorLabel = ColorLabel.None;
                picture.CurationStatus = CurationStatus.Unflagged;
                return;
            }

            // 1. Rating
            var ratingVal = 0;
            var ratingAttr = desc.Attribute(xmp + "Rating");
            if (ratingAttr != null) {
                int.TryParse(ratingAttr.Value, out ratingVal);
            } else {
                var ratingElem = desc.Element(xmp + "Rating");
                if (ratingElem != null) {
                    int.TryParse(ratingElem.Value, out ratingVal);
                }
            }
            picture.Rating = Math.Clamp(ratingVal, 0, 5);

            // 2. Color Label
            var labelStr = "";
            var labelAttr = desc.Attribute(xmp + "Label");
            if (labelAttr != null) {
                labelStr = labelAttr.Value;
            } else {
                var labelElem = desc.Element(xmp + "Label");
                if (labelElem != null) {
                    labelStr = labelElem.Value;
                }
            }
            picture.ColorLabel = ParseColorLabel(labelStr);

            // 3. Curation Status (xmpDM:pick)
            var xmpDM = XNamespace.Get("http://ns.adobe.com/xmp/1.0/DynamicMedia/");
            var pickAttrVal = desc.Attribute(xmpDM + "pick")?.Value ?? desc.Element(xmpDM + "pick")?.Value;

            if (pickAttrVal != null) {
                if (int.TryParse(pickAttrVal, out var pickVal)) {
                    picture.CurationStatus = pickVal switch {
                        1 => CurationStatus.Flagged,
                        -1 => CurationStatus.Rejected,
                        _ => CurationStatus.Unflagged
                    };
                } else {
                    picture.CurationStatus = CurationStatus.Unflagged;
                }
            } else {
                picture.CurationStatus = CurationStatus.Unflagged;
            }

            // 4. CreateDate -> CapturedAt
            var createDateStr = "";
            var createDateAttr = desc.Attribute(xmp + "CreateDate");
            if (createDateAttr != null) {
                createDateStr = createDateAttr.Value;
            } else {
                var createDateElem = desc.Element(xmp + "CreateDate");
                if (createDateElem != null) {
                    createDateStr = createDateElem.Value;
                }
            }
            if (DateTime.TryParse(createDateStr, out var createDate)) {
                picture.CapturedAt = createDate;
            }

            // 5. Keywords (dc:subject and lr:hierarchicalSubject)
            var keywords = new List<string>();
            var dcSubject = desc.Element(dc + "subject");
            if (dcSubject != null) {
                var bag = dcSubject.Element(rdf + "Bag");
                if (bag != null) {
                    foreach (var li in bag.Elements(rdf + "li")) {
                        var val = li.Value?.Trim();
                        if (!string.IsNullOrEmpty(val) && !keywords.Contains(val)) {
                            keywords.Add(val);
                        }
                    }
                }
            }

            var lrHierarchical = desc.Element(lr + "hierarchicalSubject");
            if (lrHierarchical != null) {
                var bag = lrHierarchical.Element(rdf + "Bag");
                if (bag != null) {
                    foreach (var li in bag.Elements(rdf + "li")) {
                        var val = li.Value?.Trim();
                        if (!string.IsNullOrEmpty(val)) {
                            var normalizedVal = val.Replace('/', '|').Trim('|').Trim();
                            if (!string.IsNullOrEmpty(normalizedVal) && !keywords.Contains(normalizedVal)) {
                                keywords.Add(normalizedVal);
                            }
                        }
                    }
                }
            }
            picture.Keywords = keywords;
        } catch (Exception ex) {
            Log.Error(ex, "Failed to load XMP metadata for picture {Name} from {Path}", picture.Name, xmpPath);
        }
    }

    public async Task SaveMetadataAsync(Picture picture) {
        if (picture.SubFolder == null) {
            pathService.PopulatePaths(picture);
        }

        if (picture.SubFolder == null || string.IsNullOrEmpty(picture.SubFolder.Raw)) {
            return;
        }

        var xmpPath = fileSystem.Path.ChangeExtension(picture.SubFolder.Raw, ".xmp");

        try {
            XDocument doc;
            XElement? desc = null;

            if (fileSystem.File.Exists(xmpPath)) {
                string content = "";
                const int maxRetriesRead = 5;
                for (int i = 0; i < maxRetriesRead; i++) {
                    try {
                        using (var stream = fileSystem.FileStream.New(xmpPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var reader = new StreamReader(stream)) {
                            content = await reader.ReadToEndAsync();
                        }
                        break;
                    } catch (IOException) when (i < maxRetriesRead - 1) {
                        await Task.Delay(100);
                    }
                }
                
                doc = XDocument.Parse(content);
                var foundDesc = doc.Descendants(rdf + "Description").FirstOrDefault();
                if (foundDesc == null) {
                    var newDesc = new XElement(rdf + "Description", new XAttribute(rdf + "about", ""));
                    var rdfElement = doc.Descendants(rdf + "RDF").FirstOrDefault();
                    if (rdfElement != null) {
                        rdfElement.Add(newDesc);
                        desc = newDesc;
                    } else {
                        doc = CreateNewXmpDocument(out var outDesc);
                        desc = outDesc;
                    }
                } else {
                    desc = foundDesc;
                }
            } else {
                doc = CreateNewXmpDocument(out var outDesc);
                desc = outDesc;
            }

            EnsureNamespaceAttributes(desc);

            var xmpDM = XNamespace.Get("http://ns.adobe.com/xmp/1.0/DynamicMedia/");

            // Clean up existing elements to prevent element/attribute duplication
            desc.Element(xmp + "CreatorTool")?.Remove();
            desc.Element(xmp + "Rating")?.Remove();
            desc.Element(xmp + "Label")?.Remove();
            desc.Element(photoshop + "Urgency")?.Remove();
            desc.Element(xmpDM + "pick")?.Remove();
            desc.Element(xmpDM + "good")?.Remove();
            desc.Element(xmp + "ModifyDate")?.Remove();
            desc.Element(xmp + "MetadataDate")?.Remove();
            desc.Element(xmp + "CreateDate")?.Remove();

            // Set/update attributes
            desc.SetAttributeValue(xmp + "CreatorTool", "Picturebot");
            desc.SetAttributeValue(xmp + "Rating", picture.Rating.ToString());
            desc.SetAttributeValue(xmp + "Label", picture.ColorLabel == ColorLabel.None ? "" : picture.ColorLabel.ToString());
            desc.Attribute(photoshop + "Urgency")?.Remove();

            // Write xmpDM:pick and xmpDM:good for Lightroom compatibility
            if (picture.CurationStatus == CurationStatus.Flagged) {
                desc.SetAttributeValue(xmpDM + "pick", "1");
                desc.SetAttributeValue(xmpDM + "good", "true");
            } else if (picture.CurationStatus == CurationStatus.Rejected) {
                desc.SetAttributeValue(xmpDM + "pick", "-1");
                desc.SetAttributeValue(xmpDM + "good", "false");
            } else {
                desc.SetAttributeValue(xmpDM + "pick", "0");
                desc.Attribute(xmpDM + "good")?.Remove();
            }

            // Process and write flat and hierarchical keywords
            var flatKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var hierarchicalKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (picture.Keywords != null) {
                foreach (var kw in picture.Keywords) {
                    if (string.IsNullOrWhiteSpace(kw)) continue;

                    // Normalize to pipe |
                    var normalizedKw = kw.Replace('/', '|').Trim('|').Trim();
                    hierarchicalKeywords.Add(normalizedKw);

                    // Add individual segments for dc:subject flat tags
                    var segments = normalizedKw.Split('|', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var seg in segments) {
                        flatKeywords.Add(seg.Trim());
                    }
                }
            }

            // Remove existing elements to prevent duplicates
            desc.Element(dc + "subject")?.Remove();
            desc.Element(lr + "hierarchicalSubject")?.Remove();

            if (flatKeywords.Any()) {
                var bag = new XElement(rdf + "Bag",
                    flatKeywords.Select(k => new XElement(rdf + "li", k))
                );
                desc.Add(new XElement(dc + "subject", bag));
            }

            if (hierarchicalKeywords.Any()) {
                var bag = new XElement(rdf + "Bag",
                    hierarchicalKeywords.Select(k => new XElement(rdf + "li", k))
                );
                desc.Add(new XElement(lr + "hierarchicalSubject", bag));
            }

            var nowStr = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            desc.SetAttributeValue(xmp + "ModifyDate", nowStr);
            desc.SetAttributeValue(xmp + "MetadataDate", nowStr);
            if (desc.Attribute(xmp + "CreateDate") == null) {
                desc.SetAttributeValue(xmp + "CreateDate", picture.CapturedAt.ToString("yyyy-MM-ddTHH:mm:ss"));
            }

            UpdateTitleElement(desc, picture.Name);

            var parentDir = fileSystem.Path.GetDirectoryName(xmpPath);
            if (!string.IsNullOrEmpty(parentDir) && !fileSystem.Directory.Exists(parentDir)) {
                fileSystem.Directory.CreateDirectory(parentDir);
            }

            // Write XML to file with retries in case other programs are locking it temporarily
            const int maxRetries = 5;
            for (int i = 0; i < maxRetries; i++) {
                try {
                    RecentWrites[xmpPath] = DateTime.UtcNow;
                    using var stream = fileSystem.FileStream.New(xmpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                    using var writer = new StreamWriter(stream);
                    await writer.WriteAsync(doc.ToString());
                    break;
                } catch (IOException) when (i < maxRetries - 1) {
                    await Task.Delay(100);
                }
            }
        } catch (Exception ex) {
            Log.Error(ex, "Failed to save XMP metadata for picture {Name} to {Path}", picture.Name, xmpPath);
        }
    }

    public async Task CreateXmpForAlbumAsync(int albumId) {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var album = await context.Nodes.OfType<Album>().FirstOrDefaultAsync(a => a.Id == albumId);
        if (album == null) return;

        var pictures = await context.Nodes
            .OfType<Picture>()
            .Where(p => p.ParentId == albumId)
            .ToListAsync();

        foreach (var picture in pictures) {
            picture.Parent = album;
        }

        pathService.PopulatePaths(pictures);

        var connection = context.Database.GetDbConnection();
        var originalState = connection.State;
        if (originalState != System.Data.ConnectionState.Open) {
            await connection.OpenAsync();
        }

        try {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT p.Id, p.CurationStatus, p.ColorLabel, p.Rating FROM pictures p INNER JOIN nodes n ON p.Id = n.Id WHERE n.ParentId = @albumId";
            
            var param = command.CreateParameter();
            param.ParameterName = "@albumId";
            param.Value = albumId;
            command.Parameters.Add(param);

            var legacyData = new Dictionary<int, (CurationStatus status, ColorLabel label, int rating)>();
            using (var reader = await command.ExecuteReaderAsync()) {
                while (await reader.ReadAsync()) {
                    var id = reader.GetInt32(0);
                    var statusStr = reader.IsDBNull(1) ? "Unflagged" : reader.GetString(1);
                    var labelStr = reader.IsDBNull(2) ? "None" : reader.GetString(2);
                    var rating = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);

                    Enum.TryParse<CurationStatus>(statusStr, out var status);
                    Enum.TryParse<ColorLabel>(labelStr, out var label);

                    legacyData[id] = (status, label, rating);
                }
            }

            foreach (var picture in pictures) {
                if (legacyData.TryGetValue(picture.Id, out var legacy)) {
                    picture.CurationStatus = legacy.status;
                    picture.ColorLabel = legacy.label;
                    picture.Rating = legacy.rating;

                    await SaveMetadataAsync(picture);
                }
            }
        } finally {
            if (originalState != System.Data.ConnectionState.Open) {
                await connection.CloseAsync();
            }
        }
    }

    private ColorLabel ParseColorLabel(string label) {
        if (string.IsNullOrEmpty(label)) return ColorLabel.None;
        if (Enum.TryParse<ColorLabel>(label, true, out var result)) {
            return result;
        }
        return ColorLabel.None;
    }



    private XDocument CreateNewXmpDocument(out XElement desc) {
        desc = new XElement(rdf + "Description",
            new XAttribute(rdf + "about", "")
        );

        var rdfElem = new XElement(rdf + "RDF", desc);
        var xmpMeta = new XElement(XNamespace.Get("adobe:ns:meta/") + "xmpmeta",
            new XAttribute(XNamespace.Xmlns + "x", "adobe:ns:meta/"),
            rdfElem
        );

        return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), xmpMeta);
    }

    private void EnsureNamespaceAttributes(XElement desc) {
        if (desc.Attribute(XNamespace.Xmlns + "xmp") == null) desc.Add(new XAttribute(XNamespace.Xmlns + "xmp", xmp.NamespaceName));
        if (desc.Attribute(XNamespace.Xmlns + "dc") == null) desc.Add(new XAttribute(XNamespace.Xmlns + "dc", dc.NamespaceName));
        if (desc.Attribute(XNamespace.Xmlns + "photoshop") == null) desc.Add(new XAttribute(XNamespace.Xmlns + "photoshop", photoshop.NamespaceName));
        var xmpDM = XNamespace.Get("http://ns.adobe.com/xmp/1.0/DynamicMedia/");
        if (desc.Attribute(XNamespace.Xmlns + "xmpDM") == null) desc.Add(new XAttribute(XNamespace.Xmlns + "xmpDM", xmpDM.NamespaceName));
        if (desc.Attribute(XNamespace.Xmlns + "lr") == null) desc.Add(new XAttribute(XNamespace.Xmlns + "lr", lr.NamespaceName));
    }

    private void UpdateTitleElement(XElement desc, string title) {
        var titleElem = desc.Element(dc + "title");
        if (titleElem == null) {
            titleElem = new XElement(dc + "title");
            desc.Add(titleElem);
        }

        var altElem = titleElem.Element(rdf + "Alt");
        if (altElem == null) {
            altElem = new XElement(rdf + "Alt");
            titleElem.Add(altElem);
        }

        var defaultLi = altElem.Elements(rdf + "li")
            .FirstOrDefault(el => el.Attribute(XNamespace.Xml + "lang")?.Value == "x-default");

        if (defaultLi == null) {
            defaultLi = new XElement(rdf + "li", new XAttribute(XNamespace.Xml + "lang", "x-default"));
            altElem.Add(defaultLi);
        }

        defaultLi.Value = title;
    }
}
