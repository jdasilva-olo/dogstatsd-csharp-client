using System;
using System.Globalization;

namespace StatsdClient
{
    internal class EventSerializer
    {
        private const int MaxSize = 8 * 1024;
        private readonly SerializerHelper _serializerHelper;

        public EventSerializer(SerializerHelper serializerHelper)
        {
            _serializerHelper = serializerHelper;
        }

        public SerializedMetric Serialize(
            string title,
            string text,
            string alertType,
            string aggregationKey,
            string sourceType,
            int? dateHappened,
            string priority,
            string hostname,
            string[] tags,
            bool truncateIfTooLong = false)
        {
            string processedTitle = SerializerHelper.EscapeContent(title);
            string processedText = SerializerHelper.EscapeContent(text);

            var serializedMetric = _serializerHelper.GetOptionalSerializedMetric();
            if (serializedMetric == null)
            {
                return null;
            }

            var builder = serializedMetric.Builder;

            builder.Append("_e{");
            builder.AppendFormat(CultureInfo.InvariantCulture, "{0}", processedTitle.Length);
            builder.Append(',');
            builder.AppendFormat(CultureInfo.InvariantCulture, "{0}", processedText.Length);
            builder.Append("}:");
            builder.Append(processedTitle);
            builder.Append('|');
            builder.Append(processedText);

            if (dateHappened != null)
            {
                builder.AppendFormat(CultureInfo.InvariantCulture, "|d:{0}", dateHappened.Value);
            }

            SerializerHelper.AppendIfNotNull(builder, "|h:", hostname);
            SerializerHelper.AppendIfNotNull(builder, "|k:", aggregationKey);
            SerializerHelper.AppendIfNotNull(builder, "|p:", priority);
            SerializerHelper.AppendIfNotNull(builder, "|s:", sourceType);
            SerializerHelper.AppendIfNotNull(builder, "|t:", alertType);

            _serializerHelper.AppendTags(builder, tags);

            if (builder.Length > MaxSize)
            {
                if (truncateIfTooLong)
                {
                    var overage = builder.Length - MaxSize;
                    if (title.Length > text.Length)
                    {
                        title = SerializerHelper.TruncateOverage(title, overage);
                    }
                    else
                    {
                        text = SerializerHelper.TruncateOverage(text, overage);
                    }

                    return Serialize(title, text, alertType, aggregationKey, sourceType, dateHappened, priority, hostname, tags, true);
                }
                else
                {
                    throw new Exception(string.Format("Event {0} payload is too big (more than 8kB)", title));
                }
            }

            return serializedMetric;
        }
    }
}