using System.Security.Principal;

namespace Zal.HelperFunctions.SpecificFunctions
{
    internal class IsAdminstratorChecker
    {
        public static bool IsAdministrator()
        {
            return (new WindowsPrincipal(WindowsIdentity.GetCurrent()))
                      .IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
