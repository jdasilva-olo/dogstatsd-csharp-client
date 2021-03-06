using System;
using System.Globalization;
using System.Text;

namespace StatsdClient
{
    internal class ServiceCheckSerializer
    {
        private const int ServiceCheckMaxSize = 8 * 1024;
        private readonly SerializerHelper _serializerHelper;

        public ServiceCheckSerializer(SerializerHelper serializerHelper)
        {
            _serializerHelper = serializerHelper;
        }

        public SerializedMetric Serialize(
            string name,
            int status,
            int? timestamp,
            string hostname,
            string[] tags,
            string serviceCheckMessage,
            bool truncateIfTooLong = false)
        {
            var serializedMetric = _serializerHelper.GetOptionalSerializedMetric();
            if (serializedMetric == null)
            {
                return null;
            }

            var builder = serializedMetric.Builder;

            string processedName = EscapeName(name);
            string processedMessage = EscapeMessage(serviceCheckMessage);

            builder.Append("_sc|");
            builder.Append(processedName);
            builder.AppendFormat(CultureInfo.InvariantCulture, "|{0}", status);

            if (timestamp != null)
            {
                builder.AppendFormat(CultureInfo.InvariantCulture, "|d:{0}", timestamp.Value);
            }

            SerializerHelper.AppendIfNotNull(builder, "|h:", hostname);

            _serializerHelper.AppendTags(builder, tags);

            // Note: this must always be appended to the result last.
            SerializerHelper.AppendIfNotNull(builder, "|m:", processedMessage);

            var truncatedMessage = TruncateMessageIfRequired(name, builder, truncateIfTooLong, processedMessage);
            if (truncatedMessage != null)
            {
                return Serialize(name, status, timestamp, hostname, tags, truncatedMessage, true);
            }

            return serializedMetric;
        }

        private static string TruncateMessageIfRequired(
            string name,
            StringBuilder builder,
            bool truncateIfTooLong,
            string processedMessage)
        {
            if (builder.Length > ServiceCheckMaxSize)
            {
                if (!truncateIfTooLong)
                {
                    throw new Exception(string.Format("ServiceCheck {0} payload is too big (more than 8kB)", name));
                }

                var overage = builder.Length - ServiceCheckMaxSize;

                if (processedMessage == null || overage > processedMessage.Length)
                {
                    throw new ArgumentException(string.Format("ServiceCheck name is too long to truncate, payload is too big (more than 8Kb) for {0}", name), "name");
                }

                return SerializerHelper.TruncateOverage(processedMessage, overage);
            }

            return null;
        }

        private static string EscapeName(string name)
        {
            name = SerializerHelper.EscapeContent(name);

            if (name.Contains("|"))
            {
                throw new ArgumentException("Name must not contain any | (pipe) characters", "name");
            }

            return name;
        }

        private static string EscapeMessage(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                return SerializerHelper.EscapeContent(message).Replace("m:", "m\\:");
            }

            return message;
        }
    }
}