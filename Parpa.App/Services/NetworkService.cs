using Microsoft.UI.Dispatching;
using Windows.Networking.Connectivity;

namespace Parpa.Services;

/// <summary>Conexión a Internet (NetworkInformation.NetworkStatusChanged). Solo avisa cuando cambia de verdad.</summary>
public sealed class NetworkService
{
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private bool _online;

    /// <summary>Nombre de la última red: SSID si es Wi-Fi, nombre del perfil si no.</summary>
    public string NetworkName { get; private set; } = string.Empty;

    public bool IsWifi { get; private set; }

    /// <summary>Señal en % (barras × 20) si hay Wi-Fi; 100 con cable; 0 sin conexión.</summary>
    public int SignalPercent
    {
        get
        {
            try
            {
                var profile = NetworkInformation.GetInternetConnectionProfile();
                if (profile is null || profile.GetNetworkConnectivityLevel() != NetworkConnectivityLevel.InternetAccess) return 0;
                return profile.IsWlanConnectionProfile ? (profile.GetSignalBars() ?? 0) * 20 : 100;
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }

    /// <summary><c>true</c> = conexión recuperada, <c>false</c> = perdida.</summary>
    public event Action<bool>? Changed;

    public void Start()
    {
        _online = Read();
        NetworkInformation.NetworkStatusChanged += _ => _dispatcher.TryEnqueue(() =>
        {
            var online = Read();
            if (online == _online) return; // el evento llega varias veces por cada cambio
            _online = online;
            Changed?.Invoke(online);
        });
    }

    private bool Read()
    {
        try
        {
            var profile = NetworkInformation.GetInternetConnectionProfile();
            if (profile is null) return false;
            IsWifi = profile.IsWlanConnectionProfile;
            var ssid = IsWifi ? profile.WlanConnectionProfileDetails?.GetConnectedSsid() : null;
            NetworkName = string.IsNullOrEmpty(ssid) ? profile.ProfileName : ssid;
            return profile.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.InternetAccess;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
