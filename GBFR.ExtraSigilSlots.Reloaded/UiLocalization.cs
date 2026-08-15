namespace GBFR.ExtraSigilSlots.Reloaded;

internal static class UiLocalization
{
    internal const int Chinese = 0;
    internal const int English = 1;

    // These full-width/CJK punctuation glyphs sit outside the broad Han range
    // loaded by CjkConfiguredDx11Hook, so keep every UI separator here.
    internal const string GlyphSeed =
        "：（），。、｜当前语言中文英文当前角色未检测到当前角色未知角色刷新因子扫描出因子数量" +
        "当前状态可修改不可修改额外槽空缺失库存槽选择因子搜索因子名称或特性" +
        "匹配的因子清空此槽取消已装备已用于额外槽所有已使用本体扩展未被使用" +
        "预设套用覆盖保存另存为管理重命名删除新建名称保存失败成功冲突直接清空" +
        "转让来源目标全选取消选择原配置角色专属可能发生已选择个勾选要这些没" +
        "当前因子被用在角色上请您替换另一个无法使用若想放入虚拟扩展栏请到对应位置" +
        "脱除再添加转移确认相关方案将发生变更是否当前菜单期间不再提示是否知道了" +
        "游戏不支持战斗状态热更新因子请谅解扩展当前生效数量设置重启后最多至待生效保存并" +
        "确认缩减上限超出清空相关重新变为可用库存不会删除保留全部槽位定义取消发生变化" +
        "预设始终保留高位以后扩展再次套用" +
        "次从都仍下" +
        "主角古兰姬塔卡塔莉娜拉卡姆伊欧欧根萝赛塔菲莉兰斯洛特巴恩珀西瓦尔" +
        "齐格飞夏洛特尤达拉哈娜露梅巴萨拉卡塞达冈达葛萨卡莉奥丝特罗伊德" +
        "希耶提索恩圣德芬伽兰查玛琪拉菲菈贝阿朵丽丝尤斯提斯芙劳菲迪埃尔";

    internal static readonly uint[] KnownCharacterHashes =
    [
        0x2A26B1B2,
        0xA4ACBA76,
        0x18E2F9F9,
        0x079DF0CC,
        0x4D0A60C3,
        0xDD7A151E,
        0xC8616284,
        0xC3FFD418,
        0x22E437E5,
        0x2EBE91D5,
        0xBDEF7181,
        0x627BCB0D,
        0xFD3BE362,
        0xFC6CDF7B,
        0xE7053919,
        0x978E4B18,
        0x0D21B430,
        0xF0EB77EF,
        0xAA66178A,
        0xA3A3CB2F,
        0x718E1A14,
        0x296471BE,
        0xBAD16E3B,
        0x1BB37EF0,
        0x25D46F4B,
        0x9A8AF295,
        0x9B15CFB1,
        0x646C3168,
        0x74DD4C79,
    ];

    internal static bool IsEnglish(int language) => language == English;

    internal static string CharacterName(uint hash, bool english)
    {
        if (hash == 0)
            return english ? "No current character detected" : "未检测到当前角色";
        (string Chinese, string English) names = hash switch
        {
            0x2A26B1B2 => ("古兰", "Gran"),
            0xA4ACBA76 => ("姬塔", "Djeeta"),
            0x18E2F9F9 => ("卡塔莉娜", "Katalina"),
            0x079DF0CC => ("拉卡姆", "Rackam"),
            0x4D0A60C3 => ("伊欧", "Io"),
            0xDD7A151E => ("欧根", "Eugen"),
            0xC8616284 => ("萝赛塔", "Rosetta"),
            0xC3FFD418 => ("菲莉", "Ferry"),
            0x22E437E5 => ("兰斯洛特", "Lancelot"),
            0x2EBE91D5 => ("巴恩", "Vane"),
            0xBDEF7181 => ("珀西瓦尔", "Percival"),
            0x627BCB0D => ("齐格飞", "Siegfried"),
            0xFD3BE362 => ("夏洛特", "Charlotta"),
            0xFC6CDF7B => ("尤达拉哈", "Yodarha"),
            0xE7053919 => ("娜露梅", "Narmaya"),
            0x978E4B18 => ("冈达葛萨", "Ghandagoza"),
            0x0D21B430 => ("塞达", "Zeta"),
            0xF0EB77EF => ("巴萨拉卡", "Vaseraga"),
            0xAA66178A => ("卡莉奥丝特罗", "Cagliostro"),
            0xA3A3CB2F => ("伊德", "Id"),
            0x718E1A14 => ("圣德芬", "Sandalphon"),
            0x296471BE => ("希耶提", "Seofon"),
            0xBAD16E3B => ("索恩", "Tweyen"),
            0x1BB37EF0 => ("伽兰查", "Gallanza"),
            0x25D46F4B => ("玛琪拉菲菈", "Maglielle"),
            0x9A8AF295 => ("贝阿朵丽丝", "Beatrix"),
            0x9B15CFB1 => ("尤斯提斯", "Eustace"),
            0x646C3168 => ("芙劳", "Fraux"),
            0x74DD4C79 => ("菲迪埃尔", "Fediel"),
            _ => ("未知角色", "Unknown character"),
        };
        return english ? names.English : names.Chinese;
    }
}
