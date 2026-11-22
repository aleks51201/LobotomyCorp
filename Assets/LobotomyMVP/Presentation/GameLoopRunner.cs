using System.Collections.Generic;
using LobotomyMVP.App;
using LobotomyMVP.Data;
using LobotomyMVP.Domain.Model;
using UnityEngine;
using UnityEngine.UI;

namespace LobotomyMVP.Presentation
{
    /// Двигатель приложения: Update -> GameSession.Update; перестроение экрана по фазе.
    public sealed class GameLoopRunner : MonoBehaviour
    {
        public GameSession Session;
        private Transform _canvas;
        private readonly Dictionary<GamePhase, GameObject> _screens = new Dictionary<GamePhase, GameObject>();
        private GamePhase _shownPhase = (GamePhase)(-1);
        private int _selectedEmployeeId = -1;   // выбор пешки (для приказов)
        private int _selectedUnitId = -1;       // выбор камеры
        private int _selectedAnomalyIndex = -1; // выбор сбежавшей аномалии

        public int SelectedEmployeeId => _selectedEmployeeId;
        public int SelectedUnitId => _selectedUnitId;
        public int SelectedAnomalyIndex => _selectedAnomalyIndex;

        public void Init(Transform canvas)
        {
            _canvas = canvas;
            Session = new GameSession();
            Session.Bus.OnStateChanged += () => { }; // модель читается каждый кадр — ре-рендер по фазе
            RebuildScreen(force: true);
        }

        private void Update()
        {
            HandleHotkeys();
            Session.Update(Time.deltaTime);
            var s = Session.State;
            if (s.Phase == GamePhase.QuotaMet && _shownPhase == GamePhase.Management)
                RebuildScreen(force: true); // появляется кнопка "Завершить день"
            else
                Screens.CommonTick(this);
        }

        private void HandleHotkeys()
        {
            var k = UnityEngine.Input.keyboard; // через legacy Input для простоты
            if (UnityEngine.Input.GetKeyDown(KeyCode.Space))
                Session.SpeedIndex = Session.SpeedIndex == 0 ? 1 : 0;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1)) Session.SpeedIndex = 1;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2)) Session.SpeedIndex = 2;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3)) Session.SpeedIndex = 3;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                _selectedEmployeeId = -1; _selectedUnitId = -1; _selectedAnomalyIndex = -1;
                RebuildScreen(force: true);
            }
        }

        public void SelectEmployee(int id)
        {
            _selectedEmployeeId = id;
            _selectedAnomalyIndex = -1;
            Screens.CommonTick(this);
            RebuildContextPanel();
        }

        public void SelectUnit(int unitId)
        {
            _selectedUnitId = unitId;
            _selectedAnomalyIndex = -1;
            RebuildContextPanel();
        }

        public void SelectBreachedAnomaly(int anomalyIndex)
        {
            _selectedAnomalyIndex = anomalyIndex;
            _selectedUnitId = -1;
            RebuildContextPanel();
        }

        public void SendWorkCommand(WorkType work)
        {
            if (_selectedEmployeeId < 0 || _selectedUnitId < 0) return;
            Session.Enqueue(new GameCommand { Kind = CommandKind.AssignWork, EmployeeId = _selectedEmployeeId, UnitId = _selectedUnitId, Work = work });
        }

        public void SendSuppressCommand()
        {
            if (_selectedEmployeeId < 0 || _selectedAnomalyIndex < 0) return;
            Session.Enqueue(new GameCommand { Kind = CommandKind.Suppress, EmployeeId = _selectedEmployeeId, AnomalyIndex = _selectedAnomalyIndex });
        }

        public void RebuildScreen(bool force = false)
        {
            var phase = Session.State.Phase;
            if (!force && phase == _shownPhase) return;
            foreach (var kv in _screens) if (kv.Value != null) Destroy(kv.Value);
            _screens.Clear();
            _shownPhase = phase;
            var go = Screens.Build(phase, this, _canvas);
            if (go != null) _screens[phase] = go;
        }

        public void RebuildContextPanel()
        {
            if (_shownPhase == GamePhase.Management)
            {
                if (_screens.TryGetValue(GamePhase.Management, out var go) && go != null)
                {
                    var ctx = go.transform.Find("Context");
                    if (ctx != null) Screens.ManagementScreen.RefreshContext(ctx.gameObject, this);
                }
                // схема тоже подсвечивает выбор
                if (_screens.TryGetValue(GamePhase.Management, out var go2) && go2 != null)
                {
                    var map = go2.transform.Find("Map");
                    if (map != null) FacilityMapView.Refresh(map.GetComponent<FacilityMapView>(), this);
                }
            }
        }

        public void NotifyPhaseChanged() => RebuildScreen(force: true);
    }
}
