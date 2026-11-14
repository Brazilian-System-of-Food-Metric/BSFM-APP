using System.Text.Json;
using System.Text.Json.Serialization;

namespace BSFM.Api.Json;

/// <summary>
/// System.Text.Json recusa <c>NaN</c> e <c>Infinity</c> e lança
/// <see cref="ArgumentException"/>. O middleware classifica isso como 400
/// "Requisição inválida" — um erro de dado vira erro de contrato na tela.
///
/// Isso acontece de verdade em banco migrado da v1: usuário com altura 0 pode
/// ter IMC/TMB infinito gravado. Aqui o valor não finito é emitido como 0,
/// mantendo o JSON válido e o painel abrindo. A validação de entrada impede
/// que novos valores assim sejam gravados.
/// </summary>
public sealed class DoubleFinitoConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDouble();

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
        => writer.WriteNumberValue(double.IsFinite(value) ? value : 0);
}

/// <summary>Mesma regra para campos anuláveis (ex.: confiança de uma análise).</summary>
public sealed class DoubleNullableFinitoConverter : JsonConverter<double?>
{
    public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.Null ? null : reader.GetDouble();

    public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteNumberValue(double.IsFinite(value.Value) ? value.Value : 0);
    }
}
