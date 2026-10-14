using System.Globalization;
using System.Text;
using System.Xml;

namespace CulinaryBlog.Application.Seo;

/// <summary>
/// NFR-SEO-003 — dựng sitemap.xml theo sitemaps.org 0.9. UTF-8 không BOM; ngày dạng W3C (UTC);
/// priority theo InvariantCulture (vi-VN dùng dấu phẩy sẽ làm file sai chuẩn). XmlWriter tự escape.
/// </summary>
public static class SitemapXmlBuilder
{
    public const string Namespace = "http://www.sitemaps.org/schemas/sitemap/0.9";

    public static byte[] Build(IEnumerable<SitemapUrl> urls)
    {
        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), Indent = true };

        using var buffer = new MemoryStream();
        using (var writer = XmlWriter.Create(buffer, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("urlset", Namespace);

            foreach (var url in urls)
            {
                writer.WriteStartElement("url", Namespace);
                writer.WriteElementString("loc", Namespace, url.Loc);
                if (url.LastModified is { } lastModified)
                {
                    writer.WriteElementString("lastmod", Namespace,
                        lastModified.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
                }

                writer.WriteElementString("changefreq", Namespace, url.ChangeFrequency);
                writer.WriteElementString("priority", Namespace, url.Priority.ToString("0.0", CultureInfo.InvariantCulture));
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return buffer.ToArray();
    }
}
