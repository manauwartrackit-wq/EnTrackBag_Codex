# PowerShell 7: generate a hash for the single development deployment SQL script.
# Password input is masked and never printed or written to a file.
$secret = Read-Host 'New Administrator password (minimum 8 characters)' -AsSecureString
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
try {
    $passwordText = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    if ($passwordText.Length -lt 8) { throw 'Password must contain at least 8 characters.' }
    $salt = [Security.Cryptography.RandomNumberGenerator]::GetBytes(16)
    $key = [Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2($passwordText, $salt, 210000, [Security.Cryptography.HashAlgorithmName]::SHA512, 32)
    $bytes = [byte[]]::new(61)
    $bytes[0] = 1
    $offset = 1
    foreach ($number in @(2, 210000, 16)) {
        $part = [BitConverter]::GetBytes([uint32]$number)
        if ([BitConverter]::IsLittleEndian) { [Array]::Reverse($part) }
        [Array]::Copy($part, 0, $bytes, $offset, 4)
        $offset += 4
    }
    [Array]::Copy($salt, 0, $bytes, 13, 16)
    [Array]::Copy($key, 0, $bytes, 29, 32)
    [Convert]::ToBase64String($bytes)
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    $passwordText = $null
    $secret.Dispose()
}
