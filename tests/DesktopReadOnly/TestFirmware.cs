static class TestFirmware
{
 public static bool SkipMaterialChecks => Environment.GetEnvironmentVariable("MIDDLETON_SKIP_FIRMWARE_TESTS")=="1";
 static string Resolve(string variable,string relative)=>Environment.GetEnvironmentVariable(variable) ?? Path.Combine(Environment.GetEnvironmentVariable("MIDDLETON_TEST_REPOSITORY") ?? throw new InvalidOperationException("Use test.ps1 to provide material paths."),relative);
 public static string Official=>Resolve("MIDDLETON_OFFICIAL_FIRMWARE","research/_fw/Lennox_FW_V17.0.37.bin");
 public static string R2=>Resolve("MIDDLETON_R2_FIRMWARE","research/custom_fw/aux_no_amp_sleep_display_25.2.52_r2/Middleton_AUX-no-amp-sleep_no-10min-off_display-25.2.52_EXPERIMENTAL.bin");
}
