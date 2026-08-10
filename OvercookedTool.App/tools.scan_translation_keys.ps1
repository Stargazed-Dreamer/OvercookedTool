param(
    # 默认使用脚本所在目录；也可显式传 -Root 指定存档示例根目录
    [string]$Root = $PSScriptRoot
)

$fieldSet = New-Object 'System.Collections.Generic.HashSet[string]'
$metaSet = New-Object 'System.Collections.Generic.HashSet[string]'

Get-ChildItem $Root -Filter *.json -File | ForEach-Object {
    $txt = [System.IO.File]::ReadAllText($_.FullName, [System.Text.Encoding]::UTF8).Replace("`0", "")

    $outer = [regex]::Match($txt, '"m_Keys"\s*:\s*\[(?<arr>.*?)\]\s*,\s*"m_Entries"', 'Singleline')
    if ($outer.Success)
    {
        $keys = [regex]::Matches($outer.Groups['arr'].Value, '"(?<k>[^"]+)"') | ForEach-Object { $_.Groups['k'].Value }
        foreach ($k in $keys)
        {
            if ($k -notmatch '^Level_\d+$')
            {
                [void]$metaSet.Add($k)
            }
        }
    }

    $inners = [regex]::Matches($txt, '\\"m_Key\\"\s*:\s*\[(?<arr>.*?)\]', 'Singleline')
    foreach ($m in $inners)
    {
        $ks = [regex]::Matches($m.Groups['arr'].Value, '\\"(?<k>[^\\"]+)\\"') | ForEach-Object { $_.Groups['k'].Value }
        foreach ($k in $ks)
        {
            [void]$fieldSet.Add($k)
        }
    }
}

"FIELD_KEYS"
$fieldSet | Sort-Object
"META_KEYS"
$metaSet | Sort-Object
