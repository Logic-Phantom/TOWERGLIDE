using TowerGlide.Core.Input;

namespace TowerGlide.Input
{
    /// <summary>
    /// 입력 장치 하나(터치·키보드·게임패드·자이로)를 표현한다. 축은 다듬지 않은 원시값(-1..1)을 돌려준다.
    /// 감도·데드존·반전은 FlightInputRouter가 한 곳에서 적용한다.
    /// </summary>
    public interface IFlightInputSource
    {
        bool IsActive { get; }
        FlightInput ReadRaw();
    }
}
