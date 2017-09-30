namespace Orrery

open System

open Foundation
open UIKit
open ARKit
open OpenTK
open SceneKit
open CoreGraphics

[<Measure>] type km
[<Measure>] type m

type Body = { Name : string; Radius : float<km>; Surface : SCNMaterial }

[<Register ("ViewController")>]
type ViewController (handle:IntPtr) =
    inherit UIViewController (handle)

    let scale = 100000.0<km>/1.0<m>

    let mutable leftEyeView : ARSCNView = null

    // Matrices are column-major
    let positionFromTransform (xform : NMatrix4 ) = new SCNVector3(xform.M41, xform.M42, xform.M43)

    (* Load bitmap materials and return in array for applying to a SCNBox *)
    let planetarySystem =
       let material fname = 
          let mat = new SCNMaterial()
          mat.Diffuse.Contents <- UIImage.FromFile fname
          mat.LocksAmbientWithDiffuse <- true
          mat.DoubleSided <- true
          mat

       [
       ("earth", { Name = "Earth"; Radius = 6371.0<km>; Surface = material "Earth.png" } );
       ("moon", { Name = "Moon"; Radius = 1079.0<km>; Surface = material "moon-4k.png" }  );
       ("jupiter", { Name = "Jupiter"; Radius = 139822.<km>; Surface = material "realj2k.jpg" }) ;
       ("sun", { Name = "Sun"; Radius = 1391400.<km>; Surface = material "realj2k.jpg"})
       ] |> Map.ofList

     
    let globeModel body = 
      let globeGeometry = 
        let scaledRadius = float(body.Radius / scale) |> nfloat
        let geo = SCNSphere.Create scaledRadius
        geo.Materials <- [| body.Surface; body.Surface;body.Surface;body.Surface;body.Surface;body.Surface; |]
        geo

      new SCNNode (Geometry = globeGeometry)

    let earthMoonSystem = 
       let earth = globeModel planetarySystem.["earth"]
       let moon = globeModel planetarySystem.["moon"]

       let barycenter = new SCNNode()
       earth.Position <- new SCNVector3(0.f, 0.f, float32(4671.<km>/scale))
       barycenter.Add earth

       // initial position behind me 
       barycenter.Position <- new SCNVector3(0.f, 0.f, -1.f)
       earth.RunAction(SCNAction.RepeatActionForever(SCNAction.RotateBy(nfloat(0.f) , nfloat(Math.PI * 2.0), nfloat(0.), 2.4)))

       let moonOrbit = new SCNNode()
       moonOrbit.RunAction(SCNAction.RepeatActionForever(SCNAction.RotateBy(nfloat(0.f), nfloat(Math.PI * -2.0), nfloat(0.), 2.4 * 27. + 7. + 43./60.)))
       barycenter.Add(moonOrbit)

       // Position the moon 
       let moonDistance = 384400.0<km>
       let scaledDistance = (moonDistance / scale ) |> float32 |> fun f -> f / 5.0f
       //moon.Scale <- new SCNVector3(5.0f, 5.0f, 5.0f)
       moon.Position <- new SCNVector3(0.f, 0.f, -scaledDistance)
       moonOrbit.Add moon

       let jupiter = globeModel planetarySystem.["jupiter"]
       jupiter.Position <- new SCNVector3(0.f, 1.5f, -1.f)
       barycenter.Add(jupiter)

       let sun = globeModel planetarySystem.["sun"]
       sun.Geometry.FirstMaterial.Diffuse.Contents <- UIColor.Yellow
       sun.Position <- new SCNVector3(0.f, 18.f, -1.f)
       barycenter.Add(sun)

       barycenter


    override this.ViewDidLoad () =
      base.ViewDidLoad ()
       
      let halfWidth = this.View.Frame.Width / nfloat(2.) 
      leftEyeView <- new ARSCNView()
      leftEyeView.Frame <- new CGRect(nfloat(0.), nfloat(0.), halfWidth, this.View.Frame.Height)
      //arsceneview.DebugOptions <- ARSCNDebugOptions.ShowFeaturePoints + ARSCNDebugOptions.ShowWorldOrigin

      let rightEyeView = new ARSCNView()
      rightEyeView.Frame <- new CGRect(halfWidth, nfloat(0.), halfWidth, this.View.Frame.Height)
 
      this.View.AddSubview leftEyeView
      this.View.AddSubview rightEyeView

      // Configure ARKit 
      let configuration = new ARWorldTrackingConfiguration()
      configuration.PlaneDetection <- ARPlaneDetection.Horizontal

      rightEyeView.Session <- leftEyeView.Session
      let offsetNode = new SCNNode()
      offsetNode.Position <- new SCNVector3(0.072f, 0.f, 0.f)
      rightEyeView.Scene.RootNode.Add offsetNode
      let ems2 = earthMoonSystem.Clone()
      offsetNode.Add ems2

      earthMoonSystem |> leftEyeView.Scene.RootNode.Add

      // This method is called subsequent to `ViewDidLoad` so we know arsceneview is instantiated
      leftEyeView.Session.Run (configuration, ARSessionRunOptions.RemoveExistingAnchors)