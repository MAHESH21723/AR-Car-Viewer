using UnityEngine;
using UnityEngine.UI;

public class CarController : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public GameObject car;
    public GameObject carbody;
    public GameObject body,spoler,wheel;
    public Texture redtex;
    public Texture graytex;
    public Texture bluetex;
    public Texture purpiletex;
    bool isrotating = false;
    public AudioSource aaudio;
    bool isplaying = false;
    public Button playbutton;
    public Sprite playicon,stopicon;
    private Vector3 bodyst,spoilerst,wheelst;
    private Vector3 bodyend,spoilerend,wheelend;
    // Which material slot to change
    void Start()
    {
        bodyst = body.transform.position;
        spoilerst = spoler.transform.position;
        wheelst = wheel.transform.position;
        bodyend=new Vector3(0,1,0);
        spoilerend=new Vector3(0,0,-3);
        wheelend= new Vector3(0,0,3);
        isplaying = true;
        
    }

    // Update is called once per frame
    void Update()
    {if(isrotating)
        car.transform.Rotate(Vector3.up*20 * Time.deltaTime);
    }
    public void Maximizecar()
    {
        if (car.transform.localScale.x < 3f)
            car.transform.localScale += new Vector3(.1f, .1f, .1f);
    }
    public void Minimizecar()
    {
        if (car.transform.localScale.x > 0.5f)
            car.transform.localScale += new Vector3(-0.1f, -0.1f, -0.1f);
    }
    public void Redcar()
    {
        carbody.GetComponent<Renderer>().material.mainTexture = redtex;
    }
    public void purpilecar()
    {
        carbody.GetComponent<Renderer>().material.mainTexture = purpiletex;
    }
    public void Graycar()
    {
        carbody.GetComponent<Renderer>().material.mainTexture = graytex;
    }
    public void Bluecar()
    {
        carbody.GetComponent<Renderer>().material.mainTexture = bluetex;
    }
    public void rotate()
    {
        isrotating = !isrotating;

    }
    public void playaudio()
    {
        isplaying = !isplaying;

        if (isplaying)
        {
            playbutton.GetComponent<Image>().sprite = playicon;
            aaudio.Play();
        }
        else
        {
            playbutton.GetComponent<Image>().sprite =stopicon;
            aaudio.Pause();
        }
    }
   public void domanstrate()
    {
        body.transform.position = Vector3.MoveTowards(bodyst, bodyend, 1);
        spoler.transform.position = Vector3.MoveTowards(spoilerst, spoilerend, 1);
        wheel.transform.position = Vector3.MoveTowards(wheelst, wheelend, 1);
    }
    public void resetdemonstrate()
    {
        body.transform.position = Vector3.MoveTowards(body.transform.position,bodyst, 1);
        spoler.transform.position = Vector3.MoveTowards(spoler.transform.position,spoilerst, 1);
        wheel.transform.position = Vector3.MoveTowards(wheel.transform.position,wheelst, 1);
    }

}
