using NetFrameWork.Database;

/// <summary>
/// 日志核心
/// </summary>
public static class Log
{

    /// <summary>
    /// 写错误日志
    /// </summary>
    public static void Error(object log)
    {
        ORM.LogError(log);
    }

    /// <summary>
    /// 写操作日志
    /// </summary>
    public static void Info(object log)
    {
        ORM.LogInfo(log);
    }


}
