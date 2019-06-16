using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using ScanMode = Android.Bluetooth.LE.ScanMode;


namespace Plugin.BluetoothLE.Internals
{
    public class AdapterContext
    {
        readonly BluetoothManager manager;
        PreLollipopScanCallback oldCallbacks;
        LollipopScanCallback callbacks;


        public AdapterContext(BluetoothManager manager)
        {
            this.manager = manager;
            this.Devices = new DeviceManager(manager);
        }


        public DeviceManager Devices { get; }


        public IObservable<ScanResult> Scan(ScanConfig config)
        {
            this.Devices.Clear();
            var obs = CrossBleAdapter.AndroidConfiguration.UseNewScanner
                ? this.NewScan(config)
                : this.PreLollipopScan(config);

            return obs;
        }


        public void StopScan()
        {
            if (this.callbacks != null)
            {
                this.manager.Adapter.BluetoothLeScanner?.StopScan(this.callbacks);
                this.callbacks = null;
            }

            if (this.oldCallbacks != null)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                this.manager.Adapter.StopLeScan(this.oldCallbacks);
#pragma warning restore CS0618 // Type or member is obsolete
                this.oldCallbacks = null;
            }
        }


        protected virtual IObservable<ScanResult> NewScan(ScanConfig config) => Observable.Create<ScanResult>(ob =>
        {
            this.callbacks = new LollipopScanCallback((native, rssi, sr) =>
            {
                var scanResult = this.ToScanResult(native, rssi, new AdvertisementData(sr));
                ob.OnNext(scanResult);
            });

            var builder = new ScanSettings.Builder();
            var scanMode = this.ToNative(config.ScanType);
            builder.SetScanMode(scanMode);
            if (CrossBleAdapter.AndroidConfiguration.AdvancedScannerSettings)
            {
                if (config.MatchMode != null)
                {
                    builder.SetMatchMode(this.ToNative(config.MatchMode.Value));
                }

                if (config.NumOfMatches != null)
                {
                    builder.SetNumOfMatches((int)this.ToNative(config.NumOfMatches.Value));
                }
            }

            var scanFilters = new List<ScanFilter>();
            if (config.ServiceUuids != null && config.ServiceUuids.Count > 0)
            {
                foreach (var guid in config.ServiceUuids)
                {
                    var uuid = guid.ToParcelUuid();
                    scanFilters.Add(new ScanFilter.Builder()
                        .SetServiceUuid(uuid)
                        .Build()
                    );
                }
            }

            if (config.AndroidUseScanBatching && this.manager.Adapter.IsOffloadedScanBatchingSupported)
                builder.SetReportDelay(100);

            this.manager.Adapter.BluetoothLeScanner.StartScan(
                scanFilters,
                builder.Build(),
                this.callbacks
            );

            return () => this.manager.Adapter.BluetoothLeScanner?.StopScan(this.callbacks);
        });


        protected virtual IObservable<ScanResult> PreLollipopScan(ScanConfig config) => Observable.Create<ScanResult>(ob =>
        {
            this.oldCallbacks = new PreLollipopScanCallback((native, rssi, sr) =>
            {
                var ad = new AdvertisementData(sr);
                if (this.IsFiltered(ad, config))
                {
                    var scanResult = this.ToScanResult(native, rssi, ad);
                    ob.OnNext(scanResult);
                }
            });
#pragma warning disable CS0618 // Type or member is obsolete
            this.manager.Adapter.StartLeScan(this.oldCallbacks);

            return () => this.manager.Adapter.StopLeScan(this.oldCallbacks);
#pragma warning restore CS0618 // Type or member is obsolete
        });


        protected bool IsFiltered(AdvertisementData ad, ScanConfig config)
        {
            if (config.ServiceUuids == null ||
                !config.ServiceUuids.Any() ||
                ad.ServiceUuids == null ||
                !ad.ServiceUuids.Any())
                return true;

            foreach (var adUuid in ad.ServiceUuids)
            {
                foreach (var uuid in config.ServiceUuids)
                {
                    if (uuid.Equals(adUuid))
                        return true;
                }
            }

            return false;
        }

        protected ScanResult ToScanResult(BluetoothDevice native, int rssi, IAdvertisementData ad)
        {
            var dev = this.Devices.GetDevice(native);
            var result = new ScanResult(dev, rssi, ad);
            return result;
        }


        protected virtual ScanMode ToNative(BleScanType scanType)
        {
            switch (scanType)
            {
                case BleScanType.Background:
                case BleScanType.LowPowered:
                    return ScanMode.LowPower;

                case BleScanType.Balanced:
                    return ScanMode.Balanced;

                case BleScanType.LowLatency:
                    return ScanMode.LowLatency;

                default:
                    throw new ArgumentException("Invalid BleScanType");
            }
        }

        protected virtual BluetoothScanMatchMode ToNative(BleMatchMode matchMode)
        {
            switch (matchMode)
            {
                case BleMatchMode.Aggressive:
                    return BluetoothScanMatchMode.Aggressive;

                case BleMatchMode.Sticky:
                    return BluetoothScanMatchMode.Sticky;

                default:
                    throw new ArgumentException("Invalid BleMatchMode");
            }
        }

        protected virtual BluetoothScanMatchNumber ToNative(BleNumOfMatches matchNumber)
        {
            switch (matchNumber)
            {
                case BleNumOfMatches.One:
                    return BluetoothScanMatchNumber.OneAdvertisement;
                case BleNumOfMatches.Few:
                    return BluetoothScanMatchNumber.FewAdvertisement;
                case BleNumOfMatches.Max:
                    return BluetoothScanMatchNumber.MaxAdvertisement;

                default:
                    throw new ArgumentException("Invalid BleNumOfMatches");
            }
        }
    }
}