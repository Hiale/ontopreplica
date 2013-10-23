﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PostInstaller {
    class Program {
        static int Main(string[] args) {
            var success = Run(args);

            Console.Read();

            return success ? 0 : 1;
        }

        private static bool Run(string[] args) {
            Console.WriteLine("Attempting to create IShellItem for file {0}...", args[0]);
            IShellLink link = (IShellLink)new CShellLink();

            //Win32Shell.SHCreateItemFromParsingName(args[0], IntPtr.Zero, Win32Shell.IShellLinkId, out link);
            IPersistFile persistFileLink = (IPersistFile)link;
            if (persistFileLink.Load(args[0], 0x00000002L) != 0) {
                Console.WriteLine("Failed to load via IPersistFile.");
                return false;
            }

            link.Resolve(IntPtr.Zero, 0);

            /*
            link.SetPath("C:\\Windows\\notepad.exe");
            link.SetArguments("");
            */

            Console.WriteLine("Querying for IPropertyStore interface...");
            IPropertyStore propStore = (IPropertyStore)link;

            PropertyKey appUserModelKey = new PropertyKey(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);

            try {
                object oldValue;
                propStore.GetValue(ref appUserModelKey, out oldValue);

                Console.WriteLine("AppUserModel.ID value: {0} (type {1})", oldValue, oldValue.GetType().AssemblyQualifiedName);
            }
            catch (Exception ex) {
#if DEBUG
                throw;
#else
                Console.WriteLine("Unable to get value of AppUserModel.ID property.");
                Console.WriteLine(ex);
#endif
            }

            try {
                Console.WriteLine("Attempting to set property 'System.AppUserModel.ID' to {0}...", args[1]);

                propStore.SetValue(ref appUserModelKey, new BStrWrapper(args[1]));
                propStore.Commit();

                //Store
                ((IPersistFile)link).Save(args[0], false);
            }
            catch (Exception ex) {
#if DEBUG
                throw;
#else
                Console.WriteLine("Unable to set value of AppUserModel.ID property.");
                Console.WriteLine(ex);
                
                return false;
#endif
            }

            Console.WriteLine("Done.");
            
            return true;
        }
    }
}
