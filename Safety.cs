public enum Verdict { Allowed, NeedsExtraConfirm, Refused }

public static class Safety
{
    const ulong LargeDisk = 256UL * 1024 * 1024 * 1024;

    public static Verdict Check(DiskCandidate d, out string key)
    {
        key = null;
        if (d == null || !d.IsSystemVerified)  { key = "refuse.unverified"; return Verdict.Refused; }
        if (d.BusType != DiskScanner.BusUsb)   { key = "refuse.not_usb";    return Verdict.Refused; }
        if (d.IsSystem || d.IsBoot)            { key = "refuse.system";     return Verdict.Refused; }
        if (d.Size == 0)                       { key = "refuse.no_media";   return Verdict.Refused; }
        if (d.Size > LargeDisk)                { key = "warn.large";        return Verdict.NeedsExtraConfirm; }
        return Verdict.Allowed;
    }
}
