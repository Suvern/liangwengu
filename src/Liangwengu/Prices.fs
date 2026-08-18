namespace Liangwengu

/// 某一计费时段的价格（单位：元 / 百万 tokens）
type PeriodPrices = {
    InputCacheHit: decimal
    InputCacheMiss: decimal
    Output: decimal
}

/// 某模型的峰谷价格表
type ModelPrices = {
    ModelId: string
    DisplayName: string
    Peak: PeriodPrices
    OffPeak: PeriodPrices
}

module Prices =

    // 数据来源: https://api-docs.deepseek.com/zh-cn/quick_start/pricing/
    // 快照日期: 2026-08-18
    let all: ModelPrices list = [
        { ModelId = "deepseek-v4-flash"; DisplayName = "Flash";
          Peak = { InputCacheHit = 0.10m; InputCacheMiss = 3.0m; Output = 9.0m };
          OffPeak = { InputCacheHit = 0.05m; InputCacheMiss = 1.5m; Output = 4.5m } }

        { ModelId = "deepseek-v4-pro"; DisplayName = "Pro";
          Peak = { InputCacheHit = 0.30m; InputCacheMiss = 9.0m; Output = 27.0m };
          OffPeak = { InputCacheHit = 0.15m; InputCacheMiss = 4.5m; Output = 13.5m } }
    ]
