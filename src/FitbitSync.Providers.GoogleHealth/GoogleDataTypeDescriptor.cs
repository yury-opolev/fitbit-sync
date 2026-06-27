using FitbitSync.Domain;

namespace FitbitSync.Providers.GoogleHealth;

// Describes how a domain MetricType maps to the Google Health API: the kebab-case dataType id used in the
// request path, the AIP-160 filter member used to bound the query by civil date, and the resolution the
// resulting samples carry.
internal sealed record GoogleDataTypeDescriptor(string DataType, string FilterMember, IntradayResolution Resolution);
