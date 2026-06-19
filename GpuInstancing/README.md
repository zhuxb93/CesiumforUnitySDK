# GpuInstancing

## 解决什么问题
大量同类对象的渲染瓶颈通常不在逻辑，而在提交批次和实例数据上传。

## 实现思路
模块把实例数据整理为连续 buffer，并用 Job 预填充矩阵、材质参数和裁剪状态，交给 BatchRendererGroup 执行。

## 基本用法
把 BRGContainer 挂到管理对象上，用 InstanceContainer 管理实例生命周期；Samples 中给出合成方块实例的接入方式。

## 依赖
Unity 2022.3、Burst、Collections。
