
namespace GeoTiles
{
    public enum VectorType
    {
        #region Polygon
        /// <summary>
        /// 建筑
        /// </summary>
        buia,
        /// <summary>
        /// 功能面
        /// </summary>
        funa,
        /// <summary>
        /// 水系面
        /// </summary>
        hyda,
        /// <summary>
        /// 绿地
        /// </summary>
        vega,
        /// <summary>
        /// 地块 (在接口中叫chinaland，但是用不上)
        /// </summary>
        land,

       
        #endregion

        #region Line
        /// <summary>
        /// 地铁线 - Polygon
        /// </summary>
        //subl,
        /// <summary>
        /// 铁路 - Polygon
        /// </summary>
        lrrl,
        /// <summary>
        /// 道路 - Polygon
        /// </summary>
        road,
      
        #endregion

        #region Text

        /// <summary>
        /// 地铁注记
        /// </summary>
        //subp,
        /// <summary>
        /// 注记
        /// </summary>
        poi,
        /// <summary>
        /// 树
        /// </summary>
        tree,
     
        /// <summary>
        /// 网格
        /// </summary>
        grid

        #endregion

    }

    public enum VectorLoadStatus
    {
        /// <summary>
        /// 没加载
        /// </summary>
        Unload,

        /// <summary>
        /// 加载中
        /// </summary>
        Loading,

        /// <summary>
        /// 已经加载完了
        /// </summary>
        Loaded,

        /// <summary>
        /// 没有数据
        /// </summary>
        NoData,

        ///// <summary>
        ///// 停止中
        ///// </summary>
        //Stopping,

        ///// <summary>
        ///// 已停止
        ///// </summary>
        //Stoped,

        /// <summary>
        /// 应该销毁
        /// </summary>
        Destroy,
    }


    public enum TileBuildingStatus
    {
        /// <summary>
        /// 隐藏
        /// </summary>
        Hidden,

        /// <summary>
        /// 自动
        /// </summary>
        Automatic,

        /// <summary>
        /// 长显
        /// </summary>
        AlwaysVisible,

        /// <summary>
        /// 混合
        /// </summary>
        Mix

    }

}