using Averio.Domain.Common;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Averio.Infrastructure.Persistence.Converters;

public sealed class CurrencyCodeConverter : ValueConverter<CurrencyCode, string>
{
    public CurrencyCodeConverter() : base(
            currency => currency.Value,
            value => CurrencyCode.From(value))
    {
    }
}