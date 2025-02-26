﻿using DualSenseAPI;
using DualSenseAPI.State;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Reflection;

namespace Demo
{
    class Program
    {
        static T Choose<T>(T[] ts, string prompt)
        {
            for (int i = 0; i < ts.Length; i++)
            {
                Console.WriteLine($"{i}: {ts[i]}");
            }
            Console.Write(prompt);

            if (ts.Length == 1)
            {
                Console.WriteLine(0);
                return ts[0];
            }
            else
            {
                int idx;
                do
                {
                    bool parseSuccess = int.TryParse(Console.ReadLine(), out idx);
                    if (!parseSuccess) idx = -1;
                } while (idx < 0 || idx >= ts.Length);

                return ts[idx];
            }
        }

        static DualSense ChooseController()
        {
            DualSense[] available = DualSense.EnumerateControllers().ToArray();
            while (available.Length == 0)
            {
                Console.WriteLine("No DualSenses connected, press any key to retry.");
                Console.ReadKey(true);
                available = DualSense.EnumerateControllers().ToArray();
            }

            return Choose(available, "Found some DualSenses, select one: ");
        }

        static void Main(string[] args)
        {
            DualSense ds = ChooseController();
            Choose(new Action<DualSense>[] { MainAsyncPolling, MainSyncBlocking }.Select(x => x.GetMethodInfo()).ToArray(),
                "Choose a demo runner: ").Invoke(null, new object[] { ds });
        }

        static void MainSyncBlocking(DualSense ds)
        {
            ds.Acquire();
            DualSenseInputState prevState = ds.InputState;
            int wheelPos = 0;

            SetInitialProperties(ds);
            DualSenseInputState dss;
            DualSenseOutputState dso;
            do
            {
                dss = ds.ReadWriteOnce();
                dso = ds.OutputState;

                if (!prevState.MicButton && dss.MicButton)
                {
                    dso.MicLed = dso.MicLed switch
                    {
                        MicLed.Off => MicLed.Pulse,
                        MicLed.Pulse => MicLed.On,
                        _ => MicLed.Off
                    };
                }

                if (!prevState.R1Button && dss.R1Button)
                {
                    dso.PlayerLed = dso.PlayerLed switch
                    {
                        PlayerLed.None => PlayerLed.Player1,
                        PlayerLed.Player1 => PlayerLed.Player2,
                        PlayerLed.Player2 => PlayerLed.Player3,
                        PlayerLed.Player3 => PlayerLed.Player4,
                        PlayerLed.Player4 => PlayerLed.All,
                        _ => PlayerLed.None
                    };
                }

                if (!prevState.L1Button && dss.L1Button)
                {
                    dso.PlayerLedBrightness = dso.PlayerLedBrightness switch
                    {
                        PlayerLedBrightness.High => PlayerLedBrightness.Low,
                        PlayerLedBrightness.Low => PlayerLedBrightness.Medium,
                        _ => PlayerLedBrightness.High
                    };
                }

                wheelPos = ProcessStateLogic(dss, ds.OutputState, wheelPos);
                prevState = dss;

                Thread.Sleep(20);
            } while (!dss.LogoButton);
            ResetToDefaultState(ds);
            ds.Release();
        }

        static void MainAsyncPolling(DualSense ds)
        {
            ds.Acquire();
            int wheelPos = 0;

            ds.OnStatePolled += (sender) =>
            {
                wheelPos = ProcessStateLogic(sender.InputState, sender.OutputState, wheelPos);
            };
            ds.OnButtonStateChanged += (sender, delta) =>
            {
                DualSenseOutputState dso = sender.OutputState;
                if (delta.MicButton == ButtonDeltaState.Pressed)
                {
                    dso.MicLed = dso.MicLed switch
                    {
                        MicLed.Off => MicLed.Pulse,
                        MicLed.Pulse => MicLed.On,
                        _ => MicLed.Off
                    };
                }

                if (delta.R1Button == ButtonDeltaState.Pressed)
                {
                    dso.PlayerLed = dso.PlayerLed switch
                    {
                        PlayerLed.None => PlayerLed.Player1,
                        PlayerLed.Player1 => PlayerLed.Player2,
                        PlayerLed.Player2 => PlayerLed.Player3,
                        PlayerLed.Player3 => PlayerLed.Player4,
                        PlayerLed.Player4 => PlayerLed.All,
                        _ => PlayerLed.None
                    };
                }

                if (delta.L1Button == ButtonDeltaState.Pressed)
                {
                    dso.PlayerLedBrightness = dso.PlayerLedBrightness switch
                    {
                        PlayerLedBrightness.High => PlayerLedBrightness.Low,
                        PlayerLedBrightness.Low => PlayerLedBrightness.Medium,
                        _ => PlayerLedBrightness.High
                    };
                }
                Console.WriteLine("Change event fired");
            };

            SetInitialProperties(ds);
            // note this polling rate is actually slower than the delay above, because it can do the processing while waiting for the next poll
            // (20ms/50Hz is actually quite fast and will clear the screen faster than it can write the data)
            ds.BeginPolling(100);
            //note that readkey is blocking, which means we know this input method is truly async
            Console.ReadKey(true);
            ds.EndPolling();
            ResetToDefaultState(ds);
            ds.Release();
        }

        static void SetInitialProperties(DualSense ds)
        {
            ds.JoystickDeadZone = 0.1f;
            ds.OutputState = new DualSenseOutputState()
            {
                LightbarBehavior = LightbarBehavior.CustomColor,
                R2Effect = new TriggerEffect.Vibrate(20, 1, 1, 1),
                L2Effect = new TriggerEffect.Section(0, 0.5f)
            };
        }

        static int ProcessStateLogic(DualSenseInputState dss, DualSenseOutputState dso, int wheelPos)
        {
            Console.Clear();

            Console.WriteLine($"LS: ({dss.LeftAnalogStick.X:F2}, {dss.LeftAnalogStick.Y:F2})");
            Console.WriteLine($"RS: ({dss.RightAnalogStick.X:F2}, {dss.RightAnalogStick.Y:F2})");
            Console.WriteLine($"Triggers: ({dss.L2:F2}, {dss.R2:F2})");
            Console.WriteLine($"Touch 1: ({dss.Touchpad1.X}, {dss.Touchpad1.Y}, {dss.Touchpad1.IsDown}, {dss.Touchpad1.Id})");
            Console.WriteLine($"Touch 2: ({dss.Touchpad2.X}, {dss.Touchpad2.Y}, {dss.Touchpad2.IsDown}, {dss.Touchpad2.Id})");
            Console.WriteLine($"Gyro: ({dss.Gyro.X}, {dss.Gyro.Y}, {dss.Gyro.Z})");
            Console.WriteLine($"Accel: ({dss.Accelerometer.X}, {dss.Accelerometer.Y}, {dss.Accelerometer.Z}); m={dss.Accelerometer.Magnitude()}");
            Console.WriteLine($"Headphone: {dss.IsHeadphoneConnected}");
            Console.WriteLine($"Battery: {dss.BatteryStatus.IsCharging}, {dss.BatteryStatus.IsFullyCharged}, {dss.BatteryStatus.Level}");

            ListPressedButtons(dss);

            dso.LeftRumble = Math.Abs(dss.LeftAnalogStick.Y);
            dso.RightRumble = Math.Abs(dss.RightAnalogStick.Y);

            dso.LightbarColor = ColorWheel(wheelPos);

            return (wheelPos + 5) % 384;
        }

        static void ResetToDefaultState(DualSense ds)
        {
            ds.OutputState.LightbarBehavior = LightbarBehavior.PulseBlue;
            ds.OutputState.PlayerLed = PlayerLed.None;
            ds.OutputState.R2Effect = TriggerEffect.Default;
            ds.OutputState.L2Effect = TriggerEffect.Default;
            ds.OutputState.MicLed = MicLed.Off;
            ds.ReadWriteOnce();
        }

        static LightbarColor ColorWheel(int position)
        {
            int r = 0, g = 0, b = 0;
            switch (position / 128)
            {
                case 0:
                    r = 127 - position % 128;   //Red down
                    g = position % 128;      // Green up
                    b = 0;                  //blue off
                    break;
                case 1:
                    g = 127 - position % 128;  //green down
                    b = position % 128;      //blue up
                    r = 0;                  //red off
                    break;
                case 2:
                    b = 127 - position % 128;  //blue down
                    r = position % 128;      //red up
                    g = 0;                  //green off
                    break;
            }
            return new LightbarColor(r / 255f, g / 255f, b / 255f);
        }

        static void ListPressedButtons(DualSenseInputState dss)
        {
            IEnumerable<string> pressedButtons = dss.GetType().GetProperties()
                .Where(p => p.Name.EndsWith("Button") && p.PropertyType == typeof(bool))
                .Where(p => (bool)p.GetValue(dss)!)
                .Select(p => p.Name.Replace("Button", ""));
            string joined = string.Join(", ", pressedButtons);
            Console.WriteLine($"Buttons: {joined}");
        }
    }
}
