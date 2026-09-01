using Cysharp.Threading.Tasks;
using UnityEngine;

public partial class DragLineElement : MonoBehaviour
{
    private static readonly int MaterialEnd = Shader.PropertyToID("_End");
    private static readonly int MaterialStart = Shader.PropertyToID("_Start");
    
    private Game Game { get; set; }
    
    private SpriteRenderer spriteRenderer;
    
    public bool IsCollected { get; private set; }
    public ChartModel.Note FromNoteModel { get; private set; }
    public ChartModel.Note ToNoteModel { get; private set; }

    private bool hasFromNote;
    private Note fromNote;
    private bool hasToNote;
    private Note toNote;
    
    private float introRatio;
    private float outroRatio;

    private float length;

    // Coincident drag notes (same screen x/y across pages) produce a zero-length
    // segment. RotationBetweenPositions then returns rotZ=0 (point up). Combined
    // with scale.x=1 and _End growing, the 0.16 sprite rasterizes as an upward bar.
    private const float MinDrawableLength = 0.0001f;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Initialize(Game game)
    {
        Game = game;
    }

    public void Dispose()
    {
        Destroy(gameObject);
    }

    public void SetData(ChartModel.Note fromNoteModel, ChartModel.Note toNoteModel)
    {
        IsCollected = false;
        
        FromNoteModel = fromNoteModel;
        ToNoteModel = toNoteModel;
        spriteRenderer.material.SetFloat(MaterialEnd, 0.0f);
        spriteRenderer.material.SetFloat(MaterialStart, 0.0f);
        UpdateTransform();
        ApplyRendererEnabled();
        spriteRenderer.sortingOrder = fromNoteModel.id;
        Game.onGameUpdate.AddListener(OnGameUpdate);
    }

    private void UpdateTransform()
    {
        if (Game.SpawnedNotes.ContainsKey(FromNoteModel.id))
        {
            if (!hasFromNote)
            {
                hasFromNote = true;
                fromNote = Game.SpawnedNotes[FromNoteModel.id];
            }
        }
        else
        {
            if (hasFromNote)
            {
                hasFromNote = false;
                fromNote = null;
            }
        }
        if (Game.SpawnedNotes.ContainsKey(ToNoteModel.id))
        {
            if (!hasToNote)
            {
                hasToNote = true;
                toNote = Game.SpawnedNotes[ToNoteModel.id];
            }
        }
        else
        {
            if (hasToNote)
            {
                hasToNote = false;
                toNote = null;
            }
        }

        // Chart positions only. Spawned transforms are still (0,0) on the first
        // onGameUpdate after pool spawn (note writes localPosition later in the
        // same event). Reading them draws a phantom segment to the origin.
        var fromNotePosition = FromNoteModel.CalculatePosition(Game.Chart);
        var toNotePosition = ToNoteModel.CalculatePosition(Game.Chart);
        
        var transform = this.transform;
        transform.localPosition = fromNotePosition;
        length = Vector3.Distance(
            fromNotePosition, 
            toNotePosition
        );
        if (length < MinDrawableLength)
        {
            // Degenerate segment: keep the object alive for Collect / pool, but do
            // not aim or scale the 0.16 sprite (rotZ=0 + scale.x=1 draws a bar).
            length = 0f;
            transform.localScale = Vector3.zero;
            return;
        }

        spriteRenderer.material.mainTextureScale = new Vector2(1.0f, length / 0.16f);
        // Aim from the same endpoints used for length. Do not copy the from-note's
        // transform rotation: Chart never bakes it, LateUpdate writes it after
        // onGameUpdate, and a paused Lab seek never gets a second update.
        transform.localEulerAngles = ChartModel.Note.RotationBetweenPositions(fromNotePosition, toNotePosition);
        transform.localScale = new Vector3(1.0f, length / 0.16f);
    }

    private void ApplyRendererEnabled()
    {
        spriteRenderer.enabled = length >= MinDrawableLength && !Game.State.Mods.Contains(Mod.HideNotes);
    }

    private void OnGameUpdate(Game _)
    {
        UpdateTransform();
        ApplyRendererEnabled();

        if (outroRatio >= 1)
        {
            Collect();
            return;
        }

        if (Game.SpawnedNotes.ContainsKey(FromNoteModel.id))
        {
            var note = Game.SpawnedNotes[FromNoteModel.id];
            if (!note.IsCleared)
            {
                if (note.Renderer is ClassicNoteRenderer classicNoteRenderer)
                {
                    var fill = classicNoteRenderer.Fill;
                    spriteRenderer.color = spriteRenderer.color.WithAlpha(fill.enabled ? fill.color.a : 0);
                }
                else
                {
                    var f = 1 - note.TimeUntilStart / (note.Model.start_time - note.Model.intro_time);
                    f = Mathf.Clamp01(f);
                    spriteRenderer.color = Color.white.WithAlpha(f);
                }
            }
        }

        var time = Game.Time;
        var introDuration = FromNoteModel.nextdraglinestoptime - FromNoteModel.nextdraglinestarttime;
        if (introDuration > 0)
        {
            introRatio = (FromNoteModel.nextdraglinestoptime - time) / introDuration;
        }
        else
        {
            introRatio = time < FromNoteModel.nextdraglinestarttime ? 1.0f : 0.0f;
        }

        var outroSpan = ToNoteModel.start_time - FromNoteModel.start_time;
        if (outroSpan > 0f)
            outroRatio = (time - FromNoteModel.start_time) / outroSpan;
        else
            outroRatio = time < FromNoteModel.start_time ? 0f : 1f;

        if (introRatio > 0 && introRatio < 1)
        {
            spriteRenderer.material.SetFloat(MaterialEnd, 1.0f - introRatio);
        }
        else if (introRatio <= 0)
        {
            spriteRenderer.material.SetFloat(MaterialEnd, 1.0f);
        }
        else
        {
            spriteRenderer.material.SetFloat(MaterialEnd, 0.0f);
        }

        if (outroRatio > 0 && outroRatio < 1)
        {
            spriteRenderer.material.SetFloat(MaterialStart, outroRatio);
        }
    }

    public void Collect()
    {
        if (IsCollected) return;
        IsCollected = true;
        
        Game.ObjectPool.CollectDragLine(this);
        Game.onGameUpdate.RemoveListener(OnGameUpdate);
        FromNoteModel = default;
        ToNoteModel = default;
        hasFromNote = default;
        fromNote = default;
        hasToNote = default;
        toNote = default;
        introRatio = default;
        outroRatio = default;
        length = default;
    }
}