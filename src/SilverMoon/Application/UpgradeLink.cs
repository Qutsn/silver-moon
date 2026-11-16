using System;
using System.Threading;
using System.Threading.Tasks;
namespace Middleton.Desktop;
public sealed record UpgradeIdentity(string Model,string Serial,string Firmware,string Hardware);
public interface IUpgradeLink : IAsyncDisposable
{
 Task<UpgradeIdentity> ReconnectAsync(CancellationToken ct);
 Task WaitForDeviceDisconnectAsync(CancellationToken ct);
 Task<GaiaAcknowledgement> CommandAsync(ushort command,ReadOnlyMemory<byte> payload,CancellationToken ct);
 Task<UpgradeMessage> ReceiveAsync(TimeSpan timeout,CancellationToken ct);
}
